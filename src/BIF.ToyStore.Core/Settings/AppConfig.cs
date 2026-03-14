namespace BIF.ToyStore.Core.Settings
{
    public class AppConfig
    {
        public int Id { get; set; } = 1;
        public string DisplayName { get; set; } = "BIF Toy Store";

        // "Hiệu chỉnh số lượng sản phẩm mỗi trang"
        public int ItemsPerPage { get; set; } = 10;

        // "Lưu lại chức năng chính lần cuối mở"
        public bool ShouldSaveLastPage { get; set; } = true;
        public string LastOpenedPage { get; set; } = "Dashboard";

        // "Nếu có thông tin đăng nhập lưu từ lần trước thì tự động đăng nhập"
        public bool RememberMe { get; set; } = false;
        public string SavedUsername { get; set; } = string.Empty;

        // "Cho phép cấu hình thông tin server"
        public int LocalServerPort { get; set; } = 5000;
        public string DatabasePath { get; set; } = "ToyStore.db";
    }
}
