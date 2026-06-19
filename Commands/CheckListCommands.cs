using System;
using System.Diagnostics;
using Autodesk.AutoCAD.Runtime;
using Exception = System.Exception;

namespace MCG_CheckList.Commands
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

        /// <summary>Lệnh hiển thị Palette (gõ MCG_Checklist trong CAD)</summary>
        [CommandMethod("MCG_Checklist", CommandFlags.Modal)]
        public void Show()
        {
            Debug.WriteLine($"{LOG_PREFIX} Lệnh MCG_Checklist được gọi.");
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
    }
}
