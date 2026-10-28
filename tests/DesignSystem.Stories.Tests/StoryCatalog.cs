using System.Reflection;
using BlazingStory.Abstractions;
using BlazingStory.Internals.Services;
using BlazingStory.Internals.Services.XmlDocComment;
using BlazingStory.Types;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Norse.DesignSystem.Stories.Tests;

/// <summary>
///     Reaches a compiled <c>.stories.razor</c> story's own template, so a lock fact renders what the catalog renders
///     rather than a hand-built copy of it. The stories component is mounted the way BlazingStory's own
///     <c>StoriesRazorDetector</c> mounts it -- under the <see cref="StoriesStore" /> and
///     <see cref="StoriesRazorDescriptor" /> cascades, plus the <see cref="IServiceProvider" /> cascade its
///     <c>Stories</c>/<c>Story</c> components read -- which registers every named story into the store; the named
///     story's template is then rendered with its own context. The store's container and story lists are internal to
///     BlazingStory, so they are read by reflection, and a BlazingStory upgrade that moves them fails here, loudly.
/// </summary>
static class StoryCatalog
{
	const BindingFlags Internal = BindingFlags.Instance | BindingFlags.NonPublic;

	/// <summary>Renders the story named <paramref name="name" /> from the stories component <typeparamref name="TStories" />.</summary>
	public static IRenderedComponent<ContainerFragment> RenderStory<TStories>(BunitContext context, string name)
		where TStories : IComponent
	{
		context.Services.AddSingleton(Substitute.For<IXmlDocComment>());
		var store = (StoriesStore)Activator.CreateInstance(typeof(StoriesStore), nonPublic: true)!;
		StoriesRazorDescriptor descriptor = new(typeof(TStories),
			typeof(TStories).GetCustomAttribute<StoriesAttribute>() ?? throw new InvalidOperationException($"{typeof(TStories)} carries no [Stories]."));
		context.Render(builder =>
		{
			builder.OpenComponent<CascadingValue<IServiceProvider>>(0);
			builder.AddComponentParameter(1, nameof(CascadingValue<>.Value), context.Services);
			builder.AddComponentParameter(2, nameof(CascadingValue<>.ChildContent), (RenderFragment)(services =>
			{
				services.OpenComponent<CascadingValue<StoriesStore>>(0);
				services.AddComponentParameter(1, nameof(CascadingValue<>.Value), store);
				services.AddComponentParameter(2, nameof(CascadingValue<>.ChildContent), (RenderFragment)(stores =>
				{
					stores.OpenComponent<CascadingValue<StoriesRazorDescriptor>>(0);
					stores.AddComponentParameter(1, nameof(CascadingValue<>.Value), descriptor);
					stores.AddComponentParameter(2, nameof(CascadingValue<>.ChildContent), (RenderFragment)(stories =>
					{
						stories.OpenComponent<TStories>(0);
						stories.CloseComponent();
					}));
					stores.CloseComponent();
				}));
				services.CloseComponent();
			}));
			builder.CloseComponent();
		});

		var story = Stories(store).SingleOrDefault(candidate => candidate.Name == name)
			?? throw new InvalidOperationException($"{typeof(TStories).Name} stages no story named \"{name}\".");
		var template = (RenderFragment<IStoryContext>)story.GetType().GetField("RenderFragment", Internal)!.GetValue(story)!;
		return context.Render(template(story.Context));
	}

	static IEnumerable<IStory> Stories(StoriesStore store)
	{
		var containers = (System.Collections.IEnumerable)typeof(StoriesStore).GetProperty("StoryContainers", Internal)!.GetValue(store)!;
		foreach (var container in containers)
			foreach (var story in (System.Collections.IEnumerable)container.GetType().GetField("Stories", Internal)!.GetValue(container)!)
				yield return (IStory)story;
	}
}
