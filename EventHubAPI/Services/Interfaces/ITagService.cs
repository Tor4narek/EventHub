using Storage.Entities;

namespace Services.Interfaces;

public interface ITagService
{
	public Task<Tag> CreateTagAsync(
		string name,
		string description,
		IReadOnlyCollection<string> examples,
		CancellationToken cancellationToken);

	public Task<Tag> GetTagAsync(
		Guid tagId,
		CancellationToken cancellationToken);

	public Task<List<Tag>> GetTagsAsync(CancellationToken cancellationToken);

	public Task<Tag> UpdateTagAsync(
		Guid tagId,
		string name,
		string description,
		IReadOnlyCollection<string> examples,
		CancellationToken cancellationToken);

	public Task DeleteTagAsync(Guid tagId,
		CancellationToken cancellationToken);
}