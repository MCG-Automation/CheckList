using System;
using System.Diagnostics;
using Autodesk.AutoCAD.Runtime;
using Exception = System.Exception;

namespace MCGCadPlugin.Commands
{
    /// <summary>
    /// Đăng ký các lệnh AutoCAD cho module CheckList.
    /// Class này CẦN public parameterless constructor — AutoCAD dùng
    /// Activator.CreateInstance(type) để tạo instance mỗi khi user gõ lệnh.
    /// Toàn bộ logic được uỷ quyền cho PaletteManager (Singleton).
    /// </summary>
    public class CheckListCommands
    {
        private const string LOG_PREFIX = "[CheckListCommands]";

        /// <summary>Lệnh hiển thị Palette (gõ MCG_Checklist_Show trong CAD)</summary>
        [CommandMethod("MCG_Checklist_Show", CommandFlags.Modal)]
        public void Show()
        {
            Debug.WriteLine($"{LOG_PREFIX} Lệnh MCG_Checklist_Show được gọi.");
            try
            {
                PaletteManager.Instance.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI: {ex.Message}");
                Debug.WriteLine($"{LOG_PREFIX} Stack trace:\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>Lệnh ẩn Palette (gõ MCG_Checklist_Hide trong CAD)</summary>
        [CommandMethod("MCG_Checklist_Hide", CommandFlags.Modal)]
        public void Hide()
        {
            Debug.WriteLine($"{LOG_PREFIX} Lệnh MCG_Checklist_Hide được gọi.");
            try
            {
                PaletteManager.Instance.Hide();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI: {ex.Message}");
                Debug.WriteLine($"{LOG_PREFIX} Stack trace:\n{ex.StackTrace}");
                throw;
            }
        }
    }
}
