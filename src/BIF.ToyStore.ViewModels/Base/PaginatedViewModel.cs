using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BIF.ToyStore.ViewModels.Base
{
    public abstract partial class PaginatedViewModel : BaseViewModel
    {
        [ObservableProperty]
        private int _pageSize = 20;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalCountLabel))]
        private int _totalCount;

        public string TotalCountLabel => $"Total: {TotalCount}";

        [ObservableProperty]
        private string? _beforeCursor;

        [ObservableProperty]
        private string? _afterCursor;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
        [NotifyCanExecuteChangedFor(nameof(LastPageCommand))]
        private bool _hasNextPage;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
        [NotifyCanExecuteChangedFor(nameof(FirstPageCommand))]
        private bool _hasPreviousPage;

        // Abstract method: subclass implement cách load data
        protected abstract Task LoadPageAsync(string? direction);

        [RelayCommand(CanExecute = nameof(HasNextPage))]
        public Task NextPageAsync() => LoadPageAsync("next");

        [RelayCommand(CanExecute = nameof(HasPreviousPage))]
        public Task PreviousPageAsync() => LoadPageAsync("prev");

        [RelayCommand(CanExecute = nameof(HasPreviousPage))]
        public async Task FirstPageAsync()
        {
            BeforeCursor = null;
            AfterCursor = null;
            await LoadPageAsync(null);
        }

        [RelayCommand(CanExecute = nameof(HasNextPage))]
        public async Task LastPageAsync()
        {
            BeforeCursor = null;
            AfterCursor = null;
            await LoadPageAsync("last");
        }

        // Helper method: gọi sau mỗi lần load xong để update pagination state
        protected void ApplyPageInfo(int totalCount, bool hasNext, bool hasPrev, string? startCursor, string? endCursor)
        {
            TotalCount = totalCount;
            HasNextPage = hasNext;
            HasPreviousPage = hasPrev;
            BeforeCursor = startCursor;
            AfterCursor = endCursor;
        }
    }
}
