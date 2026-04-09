using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace MCGCadPlugin.Services.FittingManagement
{
    public partial class FittingManagementService
    {
        private class DiscoveredFitting
        {
            public string PosNum { get; set; }
            public Point3d ArrowPoint { get; set; }
        }

        // ====================================================================
        // HELPER FUNCTIONS (Nội bộ của Balloon Engine)
        // ====================================================================
        private void DiscoverFittings(Transaction tr, BlockReference blk, Matrix3d currentTransform, HashSet<string> balloonedPos, List<DiscoveredFitting> foundFittings)
        {
            string posNum = "";
            bool foundPos = false;
            
            if (blk.AttributeCollection != null)
            {
                foreach (ObjectId attId in blk.AttributeCollection)
                {
                    AttributeReference attRef = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                    if (attRef != null && attRef.Tag.Equals("POS_NUM", StringComparison.OrdinalIgnoreCase))
                    {
                        posNum = attRef.TextString;
                        foundPos = true;
                        break;
                    }
                }
            }

            if (foundPos && !string.IsNullOrWhiteSpace(posNum) && !balloonedPos.Contains(posNum))
            {
                Point3d arrowPoint = Point3d.Origin.TransformBy(currentTransform);
                foundFittings.Add(new DiscoveredFitting { PosNum = posNum, ArrowPoint = arrowPoint });
                balloonedPos.Add(posNum);
            }

            ObjectId btrId = blk.IsDynamicBlock ? blk.DynamicBlockTableRecord : blk.BlockTableRecord;
            BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
            
            foreach (ObjectId childId in btr)
            {
                BlockReference childBlk = tr.GetObject(childId, OpenMode.ForRead) as BlockReference;
                if (childBlk != null)
                {
                    Matrix3d nextTransform = currentTransform * childBlk.BlockTransform;
                    DiscoverFittings(tr, childBlk, nextTransform, balloonedPos, foundFittings);
                }
            }
        }

        private bool IsSlotOccupied(Point3d pt, List<Point3d> occupied, double minDistance)
        {
            foreach (var occ in occupied)
                if (pt.DistanceTo(occ) < minDistance) return true;
            return false;
        }

        private void DrawMagneticMLeader(Transaction tr, BlockTableRecord btrSpace, Database db, Point3d arrowPt, Point3d balloonPt, string rawPosNum, double scale)
        {
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            string[] posNumbers = rawPosNum.Split(new char[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (posNumbers.Length == 0) return;

            Vector3d doglegDir = (balloonPt.X > arrowPt.X) ? Vector3d.XAxis : -Vector3d.XAxis;
            bool useCircleBlock = bt.Has("_TagCircle");

            using (MLeader mleader = new MLeader())
            {
                mleader.SetDatabaseDefaults();
                mleader.Scale = scale;
                mleader.ArrowSize = 3.0;
                mleader.EnableDogleg = true;
                mleader.DoglegLength = 0.001; 

                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (lt.Has("Mechanical-AM_5")) mleader.Layer = "Mechanical-AM_5";

                int leaderIndex = mleader.AddLeader();
                int leaderLineIndex = mleader.AddLeaderLine(leaderIndex);
                mleader.AddFirstVertex(leaderLineIndex, arrowPt);
                mleader.AddLastVertex(leaderLineIndex, balloonPt);
                mleader.SetDogleg(leaderIndex, doglegDir);

                if (useCircleBlock)
                {
                    mleader.ContentType = ContentType.BlockContent;
                    mleader.BlockContentId = bt["_TagCircle"];
                    mleader.BlockConnectionType = BlockConnectionType.ConnectExtents;
                    mleader.BlockPosition = balloonPt;
                }
                else
                {
                    mleader.ContentType = ContentType.MTextContent;
                    MText mText = new MText();
                    mText.SetDatabaseDefaults();
                    mText.Contents = posNumbers[0];
                    mText.TextHeight = 2.5;
                    mleader.MText = mText;
                    mleader.EnableFrameText = true;
                    mleader.TextLocation = balloonPt;
                }

                btrSpace.AppendEntity(mleader);
                tr.AddNewlyCreatedDBObject(mleader, true);

                if (useCircleBlock)
                {
                    SetBlockAttributeInternal(tr, bt["_TagCircle"], mleader, posNumbers[0]);
                }
            }

            if (useCircleBlock && posNumbers.Length > 1)
            {
                double circleSpacing = 14.0 * scale; 
                for (int i = 1; i < posNumbers.Length; i++)
                {
                    Point3d nextPt = balloonPt + doglegDir * (circleSpacing * i);
                    using (BlockReference stackedBlk = new BlockReference(nextPt, bt["_TagCircle"]))
                    {
                        stackedBlk.ScaleFactors = new Scale3d(scale);
                        LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                        if (lt.Has("Mechanical-AM_5")) stackedBlk.Layer = "Mechanical-AM_5";

                        btrSpace.AppendEntity(stackedBlk);
                        tr.AddNewlyCreatedDBObject(stackedBlk, true);

                        InjectAttributeToBlockInternal(tr, stackedBlk, posNumbers[i]);
                    }
                }
            }
        }

        private void SetBlockAttributeInternal(Transaction tr, ObjectId blockId, MLeader leader, string value)
        {
            BlockTableRecord circleBtr = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);
            ObjectId attDefId = ObjectId.Null;
            foreach (ObjectId id in circleBtr)
            {
                if (id.ObjectClass.IsDerivedFrom(RXObject.GetClass(typeof(AttributeDefinition))))
                {
                    attDefId = id; break;
                }
            }

            if (attDefId != ObjectId.Null)
            {
                AttributeDefinition attDef = (AttributeDefinition)tr.GetObject(attDefId, OpenMode.ForRead);
                AttributeReference attRef = new AttributeReference();
                attRef.SetAttributeFromBlock(attDef, Matrix3d.Identity); 
                attRef.TextString = value;
                leader.SetBlockAttribute(attDefId, attRef);
            }
        }

        private void InjectAttributeToBlockInternal(Transaction tr, BlockReference blkRef, string value)
        {
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(blkRef.BlockTableRecord, OpenMode.ForRead);
            foreach (ObjectId id in btr)
            {
                if (id.ObjectClass.IsDerivedFrom(RXObject.GetClass(typeof(AttributeDefinition))))
                {
                    AttributeDefinition attDef = (AttributeDefinition)tr.GetObject(id, OpenMode.ForRead);
                    using (AttributeReference attRef = new AttributeReference())
                    {
                        attRef.SetAttributeFromBlock(attDef, blkRef.BlockTransform);
                        attRef.TextString = value;
                        blkRef.AttributeCollection.AppendAttribute(attRef);
                        tr.AddNewlyCreatedDBObject(attRef, true);
                    }
                }
            }
        }
    }
}