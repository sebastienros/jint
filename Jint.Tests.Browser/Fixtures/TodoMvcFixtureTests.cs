namespace Jint.Tests.Browser.Fixtures;

/// <summary>
/// The same TodoMVC, four times: React, Vue 3, Preact and Svelte 5, each rendering the same markup from a
/// different rendering strategy and driven by the same steps.
/// </summary>
/// <remarks>
/// <para>
/// <b>One body, four fixtures, and that is the point.</b> React batches through a scheduler and a synthetic
/// event system, Preact writes to the DOM straight from a hook, Vue compiles the document's own markup and
/// re-renders from a reactive proxy, and Svelte compiles the component away and mutates the nodes it emitted.
/// A step that works in three and fails in one names the layer at fault; four separate test bodies would only
/// have said that a framework does not work.
/// </para>
/// <para>
/// The steps are the ones a TodoMVC has always had: add three, toggle one, filter by all three routes, delete
/// one. The filters are real <c>#/</c> links, so every case also drives a same-document navigation and the
/// <c>hashchange</c> the framework listens for.
/// </para>
/// </remarks>
public class TodoMvcFixtureTests
{
    [TestCase("todomvc-react")]
    [TestCase("todomvc-vue")]
    [TestCase("todomvc-preact")]
    [TestCase("todomvc-svelte")]
    public async Task ATodoListTakesRowsTogglesOneFiltersAndDeletes(string fixture)
    {
        await using var course = await FixtureCourse.OpenAsync(fixture);

        // It rendered at all, which is the whole of the framework's start-up: the bundle parsed, its module
        // wrapper ran, and it found a DOM to write to.
        await course.UntilAsync("document.querySelectorAll('.new-todo').length", "1");

        await course.EnterAsync(".new-todo", "buy milk");
        await course.EnterAsync(".new-todo", "write the fixture");
        await course.EnterAsync(".new-todo", "read the standard");

        await course.UntilAsync("document.querySelectorAll('.todo-list li').length", "3");
        (await course.TextsAsync(".todo-list li label")).Should().Be("buy milk|write the fixture|read the standard");

        // The field is controlled, so the framework cleared it after each row.
        (await course.PropertyAsync(".new-todo", "value")).Should().BeEmpty();

        (await course.TextAsync(".todo-count strong")).Should().Be("3");

        // Toggling is a checkbox's own activation behaviour: the box changes state, the framework hears the
        // change event, and the row comes back with a class it did not have.
        await course.ClickAtAsync(".todo-list li .toggle", 1);

        await course.UntilAsync("document.querySelectorAll('.todo-list li.completed').length", "1");
        (await course.TextAsync(".todo-list li.completed label")).Should().Be("write the fixture");
        (await course.TextAsync(".todo-count strong")).Should().Be("2");

        // The three routes, reached the way a user reaches them.
        await course.ClickAsync(".filters a[href='#/active']");
        await course.UntilAsync("document.querySelectorAll('.todo-list li').length", "2");
        (await course.TextsAsync(".todo-list li label")).Should().Be("buy milk|read the standard");

        await course.ClickAsync(".filters a[href='#/completed']");
        await course.UntilAsync("document.querySelectorAll('.todo-list li').length", "1");
        (await course.TextsAsync(".todo-list li label")).Should().Be("write the fixture");

        await course.ClickAsync(".filters a[href='#/']");
        await course.UntilAsync("document.querySelectorAll('.todo-list li').length", "3");

        // And a row goes away when its button says so.
        await course.ClickAtAsync(".todo-list li .destroy", 0);
        await course.UntilAsync("document.querySelectorAll('.todo-list li').length", "2");
        (await course.TextsAsync(".todo-list li label")).Should().Be("write the fixture|read the standard");
        (await course.TextAsync(".todo-count strong")).Should().Be("1");

        course.ShouldHaveReportedNothing();
    }
}
