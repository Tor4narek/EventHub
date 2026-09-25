using Microsoft.EntityFrameworkCore;
using Services.Interfaces;
using Storage;
using Storage.Entities;

namespace Services;

public class TagService : ITagService
{
	private readonly AppDbContext _dbContext;

	public TagService(AppDbContext dbContext)
	{
		ArgumentNullException.ThrowIfNull(dbContext);
		_dbContext = dbContext;
	}

	public async Task<Tag> CreateTagAsync(
		string name,
		string description,
		IReadOnlyCollection<string> examples,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(description);
		ArgumentNullException.ThrowIfNull(examples);

		name = name.Trim();

		var tagExists = await _dbContext.Tags
			.AnyAsync(t => t.Name == name, cancellationToken);

		if (tagExists)
		{
			throw new ArgumentException($"Тег с названием {name} уже существует.");
		}

		var tag = new Tag
		{
			Id = Guid.NewGuid(),
			Name = name,
			Description = description,
			Examples = examples.ToList()
		};

		_dbContext.Tags.Add(tag);
		await _dbContext.SaveChangesAsync(cancellationToken);

		return tag;
	}

	public async Task<Tag> GetTagAsync(
		Guid tagId,
		CancellationToken cancellationToken)
	{
		if (tagId == Guid.Empty)
		{
			throw new ArgumentException("Передан пустой id", nameof(tagId));
		}

		return await _dbContext.Tags
			.AsNoTracking()
			.FirstOrDefaultAsync(t => t.Id == tagId, cancellationToken)
			?? throw new KeyNotFoundException(
				$"Тег с Id {tagId} не найден.");
	}

	public async Task<List<Tag>> GetTagsAsync(
		CancellationToken cancellationToken)
	{
		return await _dbContext.Tags
			.AsNoTracking()
			.OrderBy(t => t.Name)
			.ToListAsync(cancellationToken);
	}

	public async Task<Tag> UpdateTagAsync(
		Guid tagId,
		string name,
		string description,
		IReadOnlyCollection<string> examples,
		CancellationToken cancellationToken)
	{
		if (tagId == Guid.Empty)
		{
			throw new ArgumentException("Передан пустой id", nameof(tagId));
		}

		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(description);
		ArgumentNullException.ThrowIfNull(examples);

		name = name.Trim();

		var tag = await _dbContext.Tags
			.FirstOrDefaultAsync(t => t.Id == tagId, cancellationToken)
			?? throw new KeyNotFoundException(
				$"Тег с Id {tagId} не найден.");

		var nameIsTaken = await _dbContext.Tags
			.AnyAsync(t => t.Name == name && t.Id != tagId, cancellationToken);

		if (nameIsTaken)
		{
			throw new ArgumentException($"Тег с названием {name} уже существует.");
		}

		tag.Name = name;
		tag.Description = description;
		tag.Examples = examples.ToList();

		await _dbContext.SaveChangesAsync(cancellationToken);

		return tag;
	}

	public async Task DeleteTagAsync(
		Guid tagId,
		CancellationToken cancellationToken)
	{
		if (tagId == Guid.Empty)
		{
			throw new ArgumentException("Передан пустой id", nameof(tagId));
		}

		var tag = await _dbContext.Tags
			.FirstOrDefaultAsync(t => t.Id == tagId, cancellationToken)
			?? throw new KeyNotFoundException($"Тег с Id {tagId} не найден.");

		var usedByUsers = await _dbContext.UserTags
			.AnyAsync(ut => ut.TagId == tagId, cancellationToken);

		var usedByEvents = await _dbContext.EventTags
			.AnyAsync(et => et.TagId == tagId, cancellationToken);

		if (usedByUsers || usedByEvents)
		{
			throw new InvalidOperationException(
				"Нельзя удалить тег, который используется пользователями или мероприятиями.");
		}

		_dbContext.Tags.Remove(tag);
		await _dbContext.SaveChangesAsync(cancellationToken);
	}
}