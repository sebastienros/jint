#if NET8_0_OR_GREATER
using Jint.Runtime;
using Jint.WebApi;
using Jint.WebApi.Messaging;

namespace Jint;

public partial class Engine
{
    public partial class AdvancedOperations
    {
        /// <summary>
        /// Creates a pair of entangled <c>MessagePort</c> objects spanning this engine and
        /// <paramref name="other"/>, so a script on one can <c>postMessage</c> to a script on the other.
        /// Requires .NET 8 or higher, and <see cref="WebApiFeatures.Messaging"/> on <b>both</b> engines.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the cross-engine form of <c>new MessageChannel()</c>. Each port is an ordinary
        /// <c>MessagePort</c> for its own engine; hand each half to the engine that owns it and the two
        /// scripts talk to each other:
        /// </para>
        /// <code>
        /// var pair = worker.Advanced.CreateMessagePortPair(host);
        /// worker.SetValue("hostPort", pair.Local);
        /// host.SetValue("workerPort", pair.Remote);
        /// </code>
        /// <para>
        /// <b>Threading — read this before using it across threads.</b>
        /// </para>
        /// <list type="number">
        /// <item><description>
        /// <b>Call this while neither engine is running.</b> It builds one port on each engine, which
        /// materializes that engine's <c>MessagePort</c> intrinsics — engine mutation like any other, and
        /// therefore not something to do to an engine another thread is currently executing. The natural place
        /// is host setup, before either engine has been handed a script.
        /// </description></item>
        /// <item><description>
        /// <b>Publish each half to its own engine's thread with your own synchronization.</b> A
        /// <c>JsValue</c> belongs to the engine that created it and may only be touched on that engine's
        /// thread; this method hands you two values belonging to two engines, and getting each one to the
        /// right thread is yours to arrange. <c>engine.SetValue(...)</c> called on that engine's thread is the
        /// usual way, and is itself an ordinary engine mutation with the ordinary rules.
        /// </description></item>
        /// <item><description>
        /// <b>After that, each engine only ever touches its own port.</b> <c>postMessage</c> serializes the
        /// message on the calling engine's thread into a record that holds nothing belonging to any engine,
        /// and enqueues a delivery job on the receiving engine's event loop — the same door a promise settle
        /// arriving from a background thread comes through. The job runs on whichever thread pumps that
        /// engine, and the deserialization, the <c>MessageEvent</c> and the listeners all happen there.
        /// <b>Nothing on the receiving engine is touched off its own pump.</b>
        /// </description></item>
        /// <item><description>
        /// <b>The receiving engine must actually be pumped.</b> A message is delivered on the receiver's next
        /// event-loop drain — the end of an <c>Execute</c>/<c>Evaluate</c>, a blocking
        /// <c>UnwrapIfPromise</c>, an <c>await</c> of <c>EvaluateAsync</c>, or the host's own
        /// <c>engine.Advanced.ProcessTasks()</c> loop. An engine nobody pumps never delivers a message, for
        /// the same reason it never fires a timer: Jint does not start threads.
        /// </description></item>
        /// <item><description>
        /// <b>A <c>RestoreGlobalSnapshot</c> on either engine ends the channel permanently.</b> A port's
        /// listeners are closures over the evaluation cycle it was created in, so delivering into it
        /// afterwards would run that dead cycle's code against the restored globals. Both ports record their
        /// engine's evaluation cycle when they are created, and every delivery job is dropped once that cycle
        /// is over. A pooled engine wants a fresh pair per cycle.
        /// </description></item>
        /// </list>
        /// <para>
        /// Passing this engine itself is allowed and gives a same-engine channel, which is exactly what
        /// <c>new MessageChannel()</c> produces.
        /// </para>
        /// </remarks>
        /// <param name="other">The engine that owns the other end of the channel.</param>
        /// <returns>The two ports, one for each engine.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// Either engine was built without <see cref="WebApiFeatures.Messaging"/>. Channel messaging is opt-in
        /// like every other web API, and a host reaching for this method on an engine that did not ask for it
        /// is told so rather than handed a port whose interface object the script cannot even see.
        /// </exception>
        public MessagePortPair CreateMessagePortPair(Engine other)
        {
            if (other is null)
            {
                Throw.ArgumentNullException(nameof(other));
            }

            RequireMessaging(_engine, "this engine");
            RequireMessaging(other, "the engine passed to CreateMessagePortPair");

            var (local, remote) = MessagePortBridge.CreatePair(_engine, _engine.Realm, other, other.Realm);
            return new MessagePortPair(local, remote);
        }

        private static void RequireMessaging(Engine engine, string which)
        {
            if ((engine._webApiFeatures & WebApiFeatures.Messaging) == WebApiFeatures.None)
            {
                Throw.InvalidOperationException(
                    $"CreateMessagePortPair requires WebApiFeatures.Messaging, and {which} was built without it. Enable it with options.UseWebApis(WebApiFeatures.Messaging).");
            }
        }
    }
}
#endif
