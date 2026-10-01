namespace Jint.Tests.Browser.Animations;

using Browser = global::Jint.Browser.Browser;
using Page = global::Jint.Browser.Page;

/// <summary>Web Animations Level 1 timing and playback, without a visual effect stack.</summary>
public sealed class WebAnimationsTests
{
    private static async Task<Page> BlankAsync(Browser browser)
    {
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='target'><span id='child'>text</span></div>");
        return page;
    }

    [Test]
    public async Task InterfacesHaveWebIdlShapesAndLengths()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAsync<string>(
            """
            (() => {
              const refused = [];
              for (const C of [AnimationTimeline, AnimationEffect]) {
                try { new C(); } catch (e) { refused.push(e.name); }
              }
              const a = new Animation();
              const e = new KeyframeEffect(null, null);
              return [
                refused.join('/'), Animation.length, DocumentTimeline.length, KeyframeEffect.length,
                AnimationPlaybackEvent.length, Element.prototype.animate.length,
                Element.prototype.getAnimations.length, Document.prototype.getAnimations.length,
                Animation.prototype.updatePlaybackRate.length, KeyframeEffect.prototype.setKeyframes.length,
                AnimationEffect.prototype.updateTiming.length,
                Object.getPrototypeOf(Animation.prototype) === EventTarget.prototype,
                Object.getPrototypeOf(Animation) === EventTarget,
                Object.getPrototypeOf(KeyframeEffect.prototype) === AnimationEffect.prototype,
                Object.getPrototypeOf(KeyframeEffect) === AnimationEffect,
                Object.getPrototypeOf(DocumentTimeline.prototype) === AnimationTimeline.prototype,
                Object.getPrototypeOf(DocumentTimeline) === AnimationTimeline,
                Object.getPrototypeOf(AnimationPlaybackEvent.prototype) === Event.prototype,
                Object.getOwnPropertyDescriptor(Animation.prototype, 'play').enumerable,
                Object.getOwnPropertyDescriptor(Animation.prototype, 'currentTime').enumerable,
                Object.getOwnPropertyDescriptor(Document.prototype, 'timeline').enumerable,
                Object.prototype.toString.call(a), Object.prototype.toString.call(e),
                a.playState, a.currentTime, a.startTime, a.effect, a.timeline === document.timeline,
                new Animation(null, null).timeline === null
              ].join('|');
            })()
            """)).Should().Be("TypeError/TypeError|0|0|1|1|1|0|0|1|1|0|true|true|true|true|true|true|true|true|true|true|[object Animation]|[object KeyframeEffect]|idle||||true|true");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("Animation.prototype.play.call({})")]
    [TestCase("Animation.prototype.commitStyles.call({})")]
    [TestCase("AnimationEffect.prototype.getTiming.call({})")]
    [TestCase("KeyframeEffect.prototype.getKeyframes.call({})")]
    [TestCase("Object.getOwnPropertyDescriptor(AnimationTimeline.prototype, 'currentTime').get.call({})")]
    [TestCase("Object.getOwnPropertyDescriptor(Animation.prototype, 'finished').get.call({})")]
    [TestCase("Object.getOwnPropertyDescriptor(Animation.prototype, 'ready').get.call({})")]
    [TestCase("Object.getOwnPropertyDescriptor(AnimationPlaybackEvent.prototype, 'currentTime').get.call({})")]
    [TestCase("Element.prototype.animate.call({}, [], 100)")]
    [TestCase("Document.prototype.getAnimations.call({})")]
    [TestCase("new KeyframeEffect()")]
    [TestCase("new KeyframeEffect(document.body)")]
    [TestCase("new KeyframeEffect({}, [])")]
    [TestCase("new Animation({})")]
    [TestCase("new Animation(null, {})")]
    [TestCase("new DocumentTimeline({originTime: Infinity})")]
    [TestCase("new AnimationPlaybackEvent('finish', {currentTime: NaN})")]
    public async Task RejectsWrongBrandsAndArguments(string expression)
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAsync<string>($"(() => {{ try {{ {expression}; return 'accepted'; }} catch (e) {{ return e.name; }} }})()"))
            .Should().Be("TypeError");
    }

    [Test]
    public async Task DocumentTimelinesShareTheFrameClockButHaveTheirOwnOrigin()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const timeline = document.timeline;
              const shifted = new DocumentTimeline({originTime: 100});
              const before = performance.now();
              const first = timeline.currentTime;
              const sameClock = first >= before && first <= performance.now();
              let frame;
              await new Promise(resolve => requestAnimationFrame(t => {
                frame = [Math.abs(timeline.currentTime - t) < 1,
                         Math.abs(timeline.currentTime - shifted.currentTime - 100) < 1];
                resolve();
              }));
              return [timeline === document.timeline, timeline instanceof AnimationTimeline,
                timeline instanceof DocumentTimeline, timeline.currentTime > first, ...frame,
                new Document().timeline.currentTime === null, sameClock].join(',');
            })()
            """)).Should().Be("true,true,true,true,true,true,true,true");
    }

    [Test]
    public async Task AnimateIsImmediatelyRelevantAndBecomesReadyAtAFrame()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const el = document.getElementById('target');
              const a = el.animate({opacity: [0, 1]}, {duration: 80, id: 'fade'});
              const initial = [a instanceof Animation, a.id, a.playState, a.pending,
                a.startTime === null, a.currentTime, el.getAnimations()[0] === a,
                document.getAnimations()[0] === a];
              const ready = await a.ready;
              const start = a.startTime;
              await new Promise(r => requestAnimationFrame(r));
              await new Promise(r => requestAnimationFrame(r));
              const result = [...initial, ready === a, !a.pending, typeof start === 'number',
                a.currentTime > 0, a.currentTime <= 80];
              a.cancel();
              return result.join(',');
            })()
            """)).Should().Be("true,fade,running,true,true,0,true,true,true,true,true,true,true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnimationsFinishWithoutAnyAnimationFrameRequest()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const a = document.body.animate({opacity: [0, 1]}, 50);
              const seen = [];
              const event = new Promise(resolve => a.onfinish = e => {
                seen.push('event');
                resolve([e instanceof AnimationPlaybackEvent, e.isTrusted,
                         e.currentTime, typeof e.timelineTime, e.target === a]);
              });
              const ready = a.ready.then(() => seen.push('ready'));
              const finished = a.finished;
              finished.then(() => seen.push('finished'));
              const result = await event;
              await ready;
              return [a.playState, a.currentTime, await finished === a, ...result, seen.join('/'),
                document.getAnimations().length].join(',');
            })()
            """)).Should().Be("finished,50,true,true,true,50,number,true,ready/finished/event,0");
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task FinishNotifiesSynchronouslyButDispatchesBeforeAnimationFrameCallbacks()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              // Long enough that the replay from zero cannot finish again before the frame on a loaded host.
              const a = document.body.animate([], 100000);
              const events = [];
              const finished = a.finished;
              a.ready.then(() => events.push('ready'));
              finished.then(() => events.push('finished'));
              a.onfinish = e => { events.push('finish:' + e.currentTime); queueMicrotask(() => events.push('microtask')); };
              a.finish();
              const immediate = [a.currentTime, a.playState, a.pending];
              a.currentTime = 0;
              const newPromise = a.finished !== finished;
              await new Promise(resolve => requestAnimationFrame(() => { events.push('frame'); resolve(); }));
              a.cancel();
              return [...immediate, newPromise, events.join('/')].join(',');
            })()
            """)).Should().Be("100000,finished,false,true,ready/finished/finish:100000/microtask/frame");
    }

    [Test]
    public async Task ZeroDurationFinishNotificationPrecedesTheNextFrameReadyTask()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const a = document.body.animate([], 0);
              const seen = [];
              a.ready.then(() => seen.push('ready'));
              a.finished.then(() => seen.push('finished'));
              await new Promise(resolve => a.onfinish = resolve);
              return seen.join(',');
            })()
            """)).Should().Be("finished,ready");
    }

    [Test]
    public async Task InactiveTimelineCanFinishWithoutCompletingItsPendingPlayTask()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const a = new Animation(new KeyframeEffect(null, [], 100), null);
              a.play();
              a.finish();
              const finished = await a.finished;
              const result = [finished === a, a.pending, a.currentTime, a.startTime === null];
              a.cancel();
              return result.join(',');
            })()
            """)).Should().Be("true,true,100,true");
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task FinishEventsSortByOriginRelativeTimeThenCompositeOrder()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const order = [];
              const late = new Animation(new KeyframeEffect(document.body, [], 100));
              const first = new Animation(new KeyframeEffect(document.body, [], 40));
              const second = new Animation(new KeyframeEffect(document.body, [], 40),
                                           new DocumentTimeline({originTime: 100}));
              late.onfinish = () => order.push('late');
              first.onfinish = () => {
                order.push('first');
                queueMicrotask(() => order.push('checkpoint'));
              };
              second.onfinish = () => order.push('second');
              late.startTime = first.startTime = -1000;
              second.startTime = -1100;
              second.finish(); late.finish(); first.finish();
              await new Promise(resolve => requestAnimationFrame(() => {
                order.push('frame'); resolve();
              }));
              return order.join(',');
            })()
            """)).Should().Be("first,checkpoint,second,late,frame");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task TransientFinishedStateDoesNotResolveAndReplayReplacesTheFinishedPromise()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const a = document.body.animate([], 100);
              await a.ready;
              let notified = false;
              const original = a.finished;
              original.then(() => notified = true);
              a.currentTime = 100;
              a.effect.updateTiming({iterations: 2});
              await Promise.resolve();
              const deferred = !notified && a.finished === original;
              a.finish();
              await original;
              a.play();
              const replaced = a.finished !== original && a.pending;
              a.cancel();
              return [deferred, notified, replaced].join(',');
            })()
            """)).Should().Be("true,true,true");
    }

    [Test]
    public async Task SwitchingPendingTasksReusesReadyAndRunningRateChangesWaitForTheFrame()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const a = document.body.animate([], 10000);
              const ready = a.ready;
              a.pause(); a.play();
              const reused = ready === a.ready;
              await ready;
              const before = a.currentTime;
              a.updatePlaybackRate(2);
              const pending = a.pending && a.playbackRate === 1 && a.ready !== ready;
              await a.ready;
              const continued = a.playbackRate === 2 && a.currentTime >= before;
              a.cancel();
              return [reused, pending, continued].join(',');
            })()
            """)).Should().Be("true,true,true");
    }

    [Test]
    public async Task PausePlayReverseAndRateChangesPreserveTheHeldTime()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const a = document.body.animate([], {duration: 1000, fill: 'both'});
              await a.ready;
              a.pause();
              const pendingPause = a.pending && a.playState === 'paused';
              await a.ready;
              a.currentTime = 400;
              const held = a.currentTime;
              await new Promise(r => requestAnimationFrame(r));
              const still = a.currentTime === held && a.startTime === null;
              a.updatePlaybackRate(2);
              const pausedRate = a.playbackRate === 2 && !a.pending;
              a.play();
              await a.ready;
              a.reverse();
              const oldRate = a.playbackRate;
              await a.ready;
              const reversed = a.playbackRate === -2;
              a.playbackRate = 0;
              const zero = a.currentTime;
              await new Promise(r => requestAnimationFrame(r));
              const frozen = a.currentTime === zero && a.playState === 'running';
              a.cancel();
              return [pendingPause, still, pausedRate, oldRate, reversed, frozen].join(',');
            })()
            """)).Should().Be("true,true,true,2,true,true");
    }

    [Test]
    public async Task SeekingCompletesAPendingPauseAndStartTimeCompletesAPendingPlay()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const a = document.body.animate([], 100);
              a.pause();
              const ready = a.ready;
              a.currentTime = 30;
              const sought = [a.pending, a.playState, a.currentTime, a.startTime === null];
              await ready;
              a.play();
              const playingReady = a.ready;
              a.startTime = document.timeline.currentTime - 30;
              const started = !a.pending && a.startTime !== null;
              await playingReady;
              a.startTime = null;
              const paused = a.playState === 'paused';
              a.cancel();
              return [...sought, started, paused].join(',');
            })()
            """)).Should().Be("false,paused,30,true,true,true");
    }

    [Test]
    public async Task CancelRejectsOldPromisesWithHandledAbortErrorsAndQueuesAnEvent()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const a = document.body.animate([], 1000);
              const oldReady = a.ready;
              const oldFinished = a.finished;
              const readyError = oldReady.catch(e => e.name);
              const finishedError = oldFinished.catch(e => e.name);
              const event = new Promise(resolve => a.oncancel = e => resolve(
                [e instanceof AnimationPlaybackEvent, e.currentTime === null, typeof e.timelineTime, e.isTrusted]));
              a.cancel();
              const reset = [a.playState, a.currentTime === null, a.startTime === null, a.pending,
                a.finished !== oldFinished, a.ready !== oldReady];
              const second = document.body.animate([], 1000);
              second.cancel(); // No script handlers: the rejected internal promises are still handled.
              return [...reset, await readyError, await finishedError, ...await event].join(',');
            })()
            """)).Should().Be("idle,true,true,false,true,true,AbortError,AbortError,true,true,number,true");
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [TestCase("a.playbackRate = 0; a.finish()", "InvalidStateError")]
    [TestCase("a.effect.updateTiming({iterations: Infinity}); a.finish()", "InvalidStateError")]
    [TestCase("a.effect.updateTiming({iterations: Infinity}); a.playbackRate = -1; a.play()", "InvalidStateError")]
    [TestCase("a.effect.updateTiming({iterations: Infinity}); a.playbackRate = -1; a.pause()", "InvalidStateError")]
    [TestCase("a.timeline = null; a.reverse()", "InvalidStateError")]
    [TestCase("a.currentTime = 5; a.currentTime = null", "TypeError")]
    [TestCase("a.currentTime = NaN", "TypeError")]
    [TestCase("a.startTime = Infinity", "TypeError")]
    [TestCase("a.playbackRate = Infinity", "TypeError")]
    [TestCase("a.updatePlaybackRate()", "TypeError")]
    public async Task PlaybackRefusesInvalidStateAndNumericInputs(string action, string expected)
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAsync<string>($$"""
            (() => {
              const a = new Animation(new KeyframeEffect(document.body, [], 100));
              try { {{action}}; return 'accepted'; } catch (e) { return e.name; }
              finally { a.cancel(); }
            })()
            """)).Should().Be(expected);
    }

    [Test]
    public async Task ComputedTimingExposesEveryFieldAndResolvesAuto()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAsync<string>(
            """
            (() => {
              const e = new KeyframeEffect(null, null);
              const t = e.getTiming(), c = e.getComputedTiming();
              return [t.duration, t.fill, t.iterations, t.iterationStart, t.delay, t.endDelay,
                t.direction, t.easing, c.duration, c.fill, c.endTime, c.activeDuration,
                c.localTime === null, c.progress === null, c.currentIteration === null,
                Object.keys(c).sort().join('/')].join('|');
            })()
            """)).Should().Be("auto|auto|1|0|0|0|normal|linear|0|none|0|0|true|true|true|activeDuration/currentIteration/delay/direction/duration/easing/endDelay/endTime/fill/iterationStart/iterations/localTime/progress");
    }

    [TestCase("{duration: 100, delay: 20, iterations: 2, endDelay: 30, fill: 'both'}", 10, 1, "0,0,250,200")]
    [TestCase("{duration: 100, delay: 20, iterations: 2, endDelay: 30, fill: 'both'}", 70, 1, "0.5,0,250,200")]
    [TestCase("{duration: 100, delay: 20, iterations: 2, endDelay: 30, fill: 'both'}", 220, 1, "1,1,250,200")]
    [TestCase("{duration: 100, fill: 'none'}", 100, 1, ",,100,100")]
    [TestCase("{duration: 100, fill: 'none'}", 100, -1, "1,0,100,100")]
    [TestCase("{duration: 100, fill: 'none'}", 0, -1, ",,100,100")]
    [TestCase("{duration: 100, iterations: 3, direction: 'alternate', fill: 'both'}", 150, 1, "0.5,1,300,300")]
    [TestCase("{duration: 100, iterations: 3, direction: 'alternate-reverse', fill: 'both'}", 125, 1, "0.25,1,300,300")]
    [TestCase("{duration: 100, direction: 'reverse', fill: 'both'}", 25, 1, "0.75,0,100,100")]
    [TestCase("{duration: 100, iterationStart: .5, iterations: 1.5, fill: 'both'}", 150, 1, "1,1,150,150")]
    [TestCase("{duration: 100, delay: -50, endDelay: -20, fill: 'both'}", 30, 1, "0.8,0,30,100")]
    [TestCase("{duration: 0, iterations: Infinity, fill: 'both'}", 1, 1, "1,Infinity,0,0")]
    [TestCase("{duration: Infinity, iterations: 0, fill: 'both'}", 1, 1, "0,0,0,0")]
    [TestCase("{duration: 100, iterations: 0, iterationStart: .5, fill: 'both'}", 1, 1, "0.5,0,0,0")]
    [TestCase("{duration: 100, delay: 20, fill: 'backwards', easing: 'step-start'}", 0, 1, "0,0,120,100")]
    [TestCase("{duration: 100, delay: 20, fill: 'backwards', easing: 'step-start'}", 20, 1, "1,0,120,100")]
    [TestCase("{duration: 100, fill: 'both', easing: 'linear(0, .8 50%, 1)'}", 25, 1, "0.4,0,100,100")]
    public async Task ComputesPhasesIterationsDirectionAndEasing(string options, double time, double rate, string expected)
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAsync<string>($$"""
            (() => {
              const effect = new KeyframeEffect(document.body, [], {{options}});
              const a = new Animation(effect);
              a.currentTime = {{time.ToString(System.Globalization.CultureInfo.InvariantCulture)}};
              a.playbackRate = {{rate.ToString(System.Globalization.CultureInfo.InvariantCulture)}};
              const c = effect.getComputedTiming();
              return [c.progress, c.currentIteration, c.endTime, c.activeDuration].join(',');
            })()
            """)).Should().Be(expected);
    }

    [TestCase("{delay: Infinity}")]
    [TestCase("{endDelay: NaN}")]
    [TestCase("{iterationStart: -1}")]
    [TestCase("{iterationStart: Infinity}")]
    [TestCase("{iterations: -1}")]
    [TestCase("{iterations: NaN}")]
    [TestCase("{duration: -1}")]
    [TestCase("{duration: NaN}")]
    [TestCase("{duration: '100'}")]
    [TestCase("{duration: 'AUTO'}")]
    [TestCase("{fill: 'invalid'}")]
    [TestCase("{direction: 'backwards'}")]
    [TestCase("{easing: 'inherit'}")]
    [TestCase("{easing: 'cubic-bezier(2, 0, 1, 1)'}")]
    [TestCase("{easing: 'steps(1, jump-none)'}")]
    public async Task TimingUpdatesAreValidatedAndAtomic(string update)
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAsync<string>($$"""
            (() => {
              const e = new KeyframeEffect(null, [], {duration: 100, fill: 'both'});
              const before = JSON.stringify(e.getTiming());
              let name;
              try { e.updateTiming({{update}}); } catch (ex) { name = ex.name; }
              return [name, before === JSON.stringify(e.getTiming())].join(',');
            })()
            """)).Should().Be("TypeError,true");
    }

    [Test]
    public async Task DerivedOptionsAreConvertedBeforeTimingValidation()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAsync<string>(
            """
            (() => {
              const seen = [];
              const options = {easing: 'invalid'};
              for (const key of ['composite', 'pseudoElement', 'id', 'timeline']) {
                Object.defineProperty(options, key, {get() {
                  seen.push(key); return undefined;
                }});
              }
              try { document.body.animate([], options); } catch (e) { seen.push(e.name); }
              return seen.join(',');
            })()
            """)).Should().Be("composite,pseudoElement,id,timeline,TypeError");
    }

    [Test]
    public async Task KeyframeArraysAreSpacedClonedAndReturnedAsFreshObjects()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAsync<string>(
            """
            (() => {
              const input = [{opacity: 0}, {opacity: .2, offset: .2, easing: 'steps(2, end)'}, {},
                             {opacity: 1, offset: .8}, {opacity: .5}];
              const e = new KeyframeEffect(document.body, input, {duration: 100, composite: 'add'});
              const copy = new KeyframeEffect(e);
              const frames = e.getKeyframes();
              input[0].opacity = 99;
              frames[0].opacity = 88;
              e.setKeyframes([{opacity: .7}]);
              const c = copy.getKeyframes();
              return [c.map(k => k.offset).join('/'), c.map(k => k.computedOffset).join('/'),
                c[0].opacity, typeof c[0].opacity, c[1].easing, c[0].composite,
                copy.composite, copy.target === document.body, copy.getTiming().duration,
                e.getKeyframes()[0].computedOffset, e.getKeyframes()[0].offset === null,
                Object.keys(c[0]).sort().join('/')].join('|');
            })()
            """)).Should().Be("/0.2//0.8/|0/0.2/0.5/0.8/1|0|string|steps(2)|auto|add|true|100|1|true|composite/computedOffset/easing/offset/opacity");
    }

    [Test]
    public async Task PropertyIndexedKeyframesMergePerPropertySpacingThenApplyLists()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAsync<string>(
            """
            (() => {
              const e = new KeyframeEffect(null, {
                opacity: [0, .5, 1], left: ['0px', '10px'],
                offset: [null, .3], easing: ['ease', 'linear'], composite: ['add', 'replace']
              });
              const f = e.getKeyframes();
              const single = new KeyframeEffect(null, {opacity: 1}).getKeyframes()[0];
              return [f.length, f.map(k => k.computedOffset).join('/'),
                f.map(k => k.offset).join('/'), f.map(k => k.easing).join('/'),
                f.map(k => k.composite).join('/'), f[0].left, !('left' in f[1]), f[2].left,
                single.computedOffset, single.opacity].join('|');
            })()
            """)).Should().Be("3|0/0.3/1|/0.3/|ease/linear/ease|add/replace/add|0px|true|10px|1|1");
    }

    [Test]
    public async Task KeyframeReadsUseDictionaryThenLexicographicPropertyOrder()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAsync<string>(
            """
            (() => {
              const seen = [];
              const frame = {};
              for (const name of ['opacity', 'left', 'offset', 'easing', 'composite', 'unknown',
                                  '--x', 'backgroundColor', 'cssFloat', 'cssOffset']) {
                Object.defineProperty(frame, name, {enumerable: true, get() {
                  seen.push(name);
                  return name === 'offset' ? 0 : name === 'easing' ? 'linear' :
                    name === 'composite' ? 'auto' : 'value';
                }});
              }
              const f = new KeyframeEffect(null, [frame]).getKeyframes()[0];
              return [seen.join('/'), !('unknown' in f), f.backgroundColor, f.cssFloat, f.cssOffset, f['--x']].join('|');
            })()
            """)).Should().Be("composite/easing/offset/--x/backgroundColor/cssFloat/cssOffset/left/opacity|true|value|value|value|value");
    }

    [TestCase("[{offset: -.1}]")]
    [TestCase("[{offset: 1.1}]")]
    [TestCase("[{offset: NaN}]")]
    [TestCase("[{offset: .8}, {offset: .2}]")]
    [TestCase("[{easing: 'wrong'}]")]
    [TestCase("[{composite: 'wrong'}]")]
    [TestCase("[42]")]
    [TestCase("42")]
    [TestCase("{easing: ['linear', 'wrong']}")]
    [TestCase("{opacity: [0, 1], offset: [0.8, 0.2]}")]
    public async Task InvalidKeyframesDoNotReplaceExistingOnes(string frames)
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAsync<string>($$"""
            (() => {
              const e = new KeyframeEffect(null, [{opacity: 1}]);
              let name;
              try { e.setKeyframes({{frames}}); } catch (ex) { name = ex.name; }
              return [name, e.getKeyframes()[0].opacity].join(',');
            })()
            """)).Should().Be("TypeError,1");
    }

    [Test]
    public async Task EasingValidationHappensAfterAllKeyframePropertyReads()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAsync<string>(
            """
            (() => {
              const seen = [];
              try {
                new KeyframeEffect(null, [
                  {get easing() {seen.push('easing'); return 'invalid'},
                   get opacity() {seen.push('first'); return 0}},
                  {get opacity() {seen.push('second'); return 1}}
                ]);
              } catch(e) { seen.push(e.name); }
              return seen.join(',');
            })()
            """)).Should().Be("easing,first,second,TypeError");
    }

    [Test]
    public async Task PseudoElementsAndCompositeAreValidated()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAsync<string>(
            """
            (() => {
              const e = new KeyframeEffect(document.body, [], {pseudoElement: ':before'});
              const pseudo = e.pseudoElement;
              let invalid, composite;
              try { e.pseudoElement = 'before'; } catch (x) { invalid = x.name; }
              try { e.composite = 'auto'; } catch (x) { composite = x.name; }
              const retained = e.pseudoElement;
              e.pseudoElement = null;
              e.composite = 'accumulate';
              return [pseudo, invalid, retained, composite, e.pseudoElement === null, e.composite].join(',');
            })()
            """)).Should().Be("::before,SyntaxError,::before,TypeError,true,accumulate");
    }

    [Test]
    public async Task GetAnimationsUsesSubtreesCompositeOrderAndRelevance()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAsync<string>(
            """
            (() => {
              const parent = document.getElementById('target'), child = document.getElementById('child');
              const a = parent.animate([], {duration: 1000, id: 'a'});
              const b = child.animate([], {duration: 1000, id: 'b'});
              const c = parent.animate([], {duration: 1000, id: 'c'});
              const names = animations => animations.map(a => a.id).join('');
              const initial = [names(parent.getAnimations()), names(parent.getAnimations({subtree: true})),
                names(document.getAnimations())];
              a.finish(); b.cancel(); c.pause(); c.currentTime = 10;
              const final = names(document.getAnimations());
              c.cancel();
              return [...initial, final].join(',');
            })()
            """)).Should().Be("ac,abc,abc,c");
    }

    [Test]
    public async Task ReplacedFillingAnimationsAreRemovedButPersistOptsOut()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        await page.EvaluateAsync(
            """
            globalThis.removed = [];
            const target = document.getElementById('target');
            globalThis.a = target.animate({opacity: [0, 1]}, {duration: 100, fill: 'forwards', id: 'a'});
            globalThis.b = target.animate({opacity: [1, 0]}, {duration: 100, fill: 'forwards', id: 'b'});
            globalThis.c = target.animate({opacity: [0, 1]}, {duration: 100, fill: 'forwards', id: 'c'});
            a.onremove = e => removed.push(e.type + ':' + e.currentTime + ':' + (e instanceof AnimationPlaybackEvent));
            b.persist();
            a.finish(); b.finish(); c.finish();
            """);
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>(
            "[a.replaceState,b.replaceState,c.replaceState,removed.join('/'),document.getAnimations().map(a=>a.id).join('')].join(',')"))
            .Should().Be("removed,persisted,active,remove:100:true,bc");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ReplacementRequiresCoverageOfEveryPropertyAndTheSamePseudoTarget()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        await page.EvaluateAsync(
            """
            const target = document.getElementById('target');
            globalThis.a = target.animate({opacity: [0, 1], left: ['0px', '1px']}, {duration: 100, fill: 'forwards'});
            globalThis.b = target.animate({opacity: [0, 1]}, {duration: 100, fill: 'forwards'});
            globalThis.c = target.animate({left: ['0px', '1px']}, {duration: 100, fill: 'forwards', pseudoElement: '::before'});
            a.finish(); b.finish(); c.finish();
            """);
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("[a.replaceState,b.replaceState,c.replaceState].join(',')")).Should().Be("active,active,active");
        await page.EvaluateAsync("c.effect.pseudoElement = null;");
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("a.replaceState")).Should().Be("removed");
    }

    [Test]
    public async Task CommitStylesIsExplicitDiscreteAndDoesNotAffectComputedStyleBeforehand()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAsync<string>(
            """
            (() => {
              const target = document.getElementById('target');
              target.style.opacity = '.2';
              const a = new Animation(new KeyframeEffect(target,
                [{opacity: 0, left: '0px'}, {opacity: .5, left: '10px'}, {opacity: 1, left: '20px'}],
                {duration: 100, fill: 'both'}));
              a.currentTime = 75;
              const computed = getComputedStyle(target).opacity;
              a.commitStyles();
              const middle = [target.style.opacity, target.style.left];
              a.currentTime = 100;
              a.commitStyles();
              const end = [target.style.opacity, target.style.left];
              a.effect.updateTiming({fill: 'none'});
              target.style.opacity = '.3';
              a.commitStyles();
              return [Number(computed), ...middle, ...end, Number(target.style.opacity)].join(',');
            })()
            """)).Should().Be("0.2,0.5,10px,1,20px,0.3");
    }

    [Test]
    public async Task DiscreteCommitsOnlyNotifyCustomElementsWhenAKeyframeValueIsAvailable()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAsync<string>(
            """
            (() => {
              const seen = [];
              customElements.define('animated-element', class extends HTMLElement {
                static observedAttributes = ['style'];
                attributeChangedCallback(name) { seen.push(name); }
              });
              const target = document.createElement('animated-element');
              document.body.appendChild(target);
              const a = new Animation(new KeyframeEffect(target, [{opacity: 1}], {duration: 100, fill: 'both'}));
              a.currentTime = 0;
              a.commitStyles();
              const initial = !target.hasAttribute('style') && seen.length === 0;
              a.currentTime = 100;
              a.commitStyles();
              return [initial, target.style.opacity, seen.join('/')].join(',');
            })()
            """)).Should().Be("true,1,style");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("null", "null", "NoModificationAllowedError")]
    [TestCase("document.body", "'::before'", "NoModificationAllowedError")]
    [TestCase("document.createElement('div')", "null", "InvalidStateError")]
    [TestCase("document.createElementNS('urn:test', 'x')", "null", "NoModificationAllowedError")]
    public async Task CommitStylesRefusesUnsupportedTargets(string target, string pseudo, string expected)
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAsync<string>($$"""
            (() => {
              const a = new Animation(new KeyframeEffect({{target}}, [], {duration: 100, pseudoElement: {{pseudo}}}));
              try { a.commitStyles(); return 'accepted'; } catch(e) { return e.name; }
            })()
            """)).Should().Be(expected);
    }

    [Test]
    public async Task PausedIdleAndZeroRateAnimationsDoNotKeepThePageBusy()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              globalThis.paused = document.body.animate([], {duration: 10000, iterations: Infinity});
              paused.pause();
              await paused.ready;
              globalThis.zero = document.body.animate([], {duration: 10000, iterations: Infinity});
              zero.playbackRate = 0;
              await zero.ready;
              globalThis.idle = new Animation();
              globalThis.inactive = new Animation(new KeyframeEffect(null, [], 100), null);
              inactive.play();
              return [paused.playState, zero.playState, idle.playState, inactive.pending].join(',');
            })()
            """)).Should().Be("paused,running,idle,true");
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<int>("1 + 1")).Should().Be(2);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task EffectTransferAndTimelineChangesReevaluatePlayback()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const e = new KeyframeEffect(document.body, [], {duration: 100, fill: 'both'});
              const a = new Animation(e);
              a.currentTime = 40;
              const b = new Animation(e, null);
              const transferred = a.effect === null && b.effect === e && e.getComputedTiming().localTime === null;
              b.play();
              const inactive = b.pending && b.startTime === null;
              b.timeline = document.timeline;
              await b.ready;
              const active = !b.pending && b.startTime !== null;
              b.timeline = null;
              const unresolved = b.currentTime === null;
              b.currentTime = 25;
              const held = b.currentTime === 25 && b.startTime === null;
              b.cancel();
              return [transferred, inactive, active, unresolved, held].join(',');
            })()
            """)).Should().Be("true,true,true,true,true");
    }

    [Test]
    public async Task ListenerExceptionsDoNotAbortAnimationEventsOrRaf()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);
        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const seen = [];
              const a = document.body.animate([], 100);
              a.onfinish = () => { seen.push('finish'); throw new Error('animation-listener'); };
              a.addEventListener('finish', () => { seen.push('second'); queueMicrotask(() => seen.push('microtask')); });
              a.finish();
              await new Promise(resolve => requestAnimationFrame(() => { seen.push('frame'); resolve(); }));
              return seen.join(',');
            })()
            """)).Should().Be("finish,second,microtask,frame");
        page.Errors.Should().Contain(e => e.Message.Contains("animation-listener", StringComparison.Ordinal));
    }
}
