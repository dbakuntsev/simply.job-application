namespace Simply.JobApplication.Services;

public interface IDemoDataService
{
    Task<bool> IsDatabaseEmptyAsync();
    Task LoadDemoDataAsync();
}
