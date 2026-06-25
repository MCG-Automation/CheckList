using System;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Exception = System.Exception;

[assembly: ExtensionApplication(typeof(MCG_CheckList.Commands.PluginEntry))]

namespace MCG_CheckList.Commands
{
    /// <summary>
    /// Entry point của plugin — AutoCAD gọi Initialize() khi load DLL.
    /// </summary>
    public class PluginEntry : IExtensionApplication
    {
        private const string LOG_PREFIX = "[PluginEntry]";

        public void Initialize()
        {
            Debug.WriteLine($"{LOG_PREFIX} Plugin loaded.");
            // Idle fires sau khi AutoCAD restore workspace xong — đây là thời điểm
            // chắc chắn nhất để force-hide palette, vì BeginQuit fire quá muộn
            // (AutoCAD đã lưu workspace state trước khi BeginQuit được gọi).
            Application.Idle += OnFirstIdle;
        }

        public void Terminate() { }

        /// <summary>
        /// Chạy 1 lần duy nhất sau khi AutoCAD khởi động xong.
        /// Ẩn palette nếu AutoCAD tự restore nó từ workspace — palette chỉ
        /// được hiện khi user chủ động gõ lệnh MCG_Checklist.
        /// </summary>
        private void OnFirstIdle(object sender, EventArgs e)
        {
            Application.Idle -= OnFirstIdle;
            Debug.WriteLine($"{LOG_PREFIX} First Idle — ẩn palette nếu AutoCAD đã restore.");
            PaletteManager.Instance.HideIfInitialized();
        }
    }

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
