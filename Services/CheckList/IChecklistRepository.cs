using MCG_CheckList.Models.CheckList;

namespace MCG_CheckList.Services.CheckList
{
    /// <summary>
    /// Giao diện định nghĩa các thao tác lưu trữ tệp tin cache ChecklistDocument dưới dạng JSON
    /// </summary>
    public interface IChecklistRepository
    {
        /// <summary>
        /// Tải ChecklistDocument từ bộ nhớ cache JSON. Trả về null nếu chưa tồn tại cache.
        /// </summary>
        /// <param name="projectNo">Mã số dự án</param>
        /// <param name="panelName">Mã số phân đoạn panel</param>
        /// <param name="discipline">Tên bộ môn kỹ thuật</param>
        /// <returns>Đối tượng ChecklistDocument hoặc null</returns>
        ChecklistDocument LoadChecklist(string projectNo, string panelName, string discipline);

        /// <summary>
        /// Ghi hoặc cập nhật đối tượng ChecklistDocument xuống tệp tin cache JSON.
        /// </summary>
        /// <param name="document">Hồ sơ checklist cần lưu trữ</param>
        void SaveChecklist(ChecklistDocument document);

        /// <summary>
        /// Xóa sạch mọi file cache liên quan tới Panel (dành cho chế độ debug/reset)
        /// </summary>
        void DeletePanelCache(string projectNo, string panelName);
    }
}