using MCGCadPlugin.Models.CheckList;

namespace MCGCadPlugin.Services.CheckList
{
    /// <summary>
    /// Giao diện dịch vụ đọc/ghi dữ liệu Checklist vào Named Object Dictionary của bản vẽ DWG.
    /// </summary>
    public interface IAutoCadChecklistService
    {
        /// <summary>Lưu toàn bộ trạng thái Checklist vào XRecord của bản vẽ đang mở.</summary>
        bool SaveChecklistToDwg(ChecklistDocument document);

        /// <summary>Đọc trạng thái Checklist từ XRecord của bản vẽ đang mở. Trả về null nếu chưa có.</summary>
        ChecklistDocument LoadChecklistFromDwg();

        /// <summary>Xóa toàn bộ dữ liệu Checklist khỏi bản vẽ.</summary>
        bool DeleteChecklistFromDwg();
    }
}
