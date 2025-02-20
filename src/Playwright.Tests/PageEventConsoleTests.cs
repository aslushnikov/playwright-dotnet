using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Microsoft.Playwright.Tests
{
    [Parallelizable(ParallelScope.Self)]
    public class PageEventConsoleTests2 : PageTestEx
    {
        [PlaywrightTest("page-event-console.spec.ts", "should work")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            IConsoleMessage message = null;
            void EventHandler(object sender, IConsoleMessage e)
            {
                message = e;
                Page.Console -= EventHandler;
            }
            Page.Console += EventHandler;
            await TaskUtils.WhenAll(
                Page.WaitForConsoleMessageAsync(),
                Page.EvaluateAsync("() => console.log('hello', 5, { foo: 'bar'})"));

            Assert.AreEqual("hello 5 JSHandle@object", message.Text);
            Assert.AreEqual("log", message.Type);
            Assert.AreEqual("hello", await message.Args.ElementAt(0).JsonValueAsync<string>());
            Assert.AreEqual(5, await message.Args.ElementAt(1).JsonValueAsync<int>());
            Assert.AreEqual("bar", (await message.Args.ElementAt(2).JsonValueAsync<JsonElement>()).GetProperty("foo").GetString());
        }

        [PlaywrightTest("page-event-console.spec.ts", "should emit same log twice")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEmitSameLogTwice()
        {
            var messages = new List<string>();

            Page.Console += (_, e) => messages.Add(e.Text);
            await Page.EvaluateAsync("() => { for (let i = 0; i < 2; ++i ) console.log('hello'); } ");

            Assert.AreEqual(new[] { "hello", "hello" }, messages.ToArray());
        }

        [PlaywrightTest("page-event-console.spec.ts", "should work for different console API calls")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForDifferentConsoleAPICalls()
        {
            var messages = new List<IConsoleMessage>();
            Page.Console += (_, e) => messages.Add(e);
            // All console events will be reported before `Page.evaluate` is finished.
            await Page.EvaluateAsync(@"() => {
                // A pair of time/timeEnd generates only one Console API call.
                console.time('calling console.time');
                console.timeEnd('calling console.time');
                console.trace('calling console.trace');
                console.dir('calling console.dir');
                console.warn('calling console.warn');
                console.error('calling console.error');
                console.log(Promise.resolve('should not wait until resolved!'));
            }");
            Assert.AreEqual(new[] { "timeEnd", "trace", "dir", "warning", "error", "log" }, messages.Select(msg => msg.Type));
            StringAssert.Contains("calling console.time", messages[0].Text);
            Assert.AreEqual(new[]
            {
                "calling console.trace",
                "calling console.dir",
                "calling console.warn",
                "calling console.error",
                "JSHandle@promise"
            }, messages.Skip(1).Select(msg => msg.Text));
        }

        [PlaywrightTest("page-event-console.spec.ts", "should not fail for window object")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotFailForWindowObject()
        {
            IConsoleMessage message = null;
            void EventHandler(object sender, IConsoleMessage e)
            {
                message = e;
                Page.Console -= EventHandler;
            }
            Page.Console += EventHandler;
            await TaskUtils.WhenAll(
                Page.EvaluateAsync("() => console.error(window)"),
                Page.WaitForConsoleMessageAsync()
            );
            Assert.AreEqual("JSHandle@object", message.Text);
        }

        [PlaywrightTest("page-event-console.spec.ts", "should trigger correct Log")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTriggerCorrectLog()
        {
            await Page.GotoAsync("about:blank");
            var (messageEvent, _) = await TaskUtils.WhenAll(
                Page.WaitForConsoleMessageAsync(),
                Page.EvaluateAsync("async url => fetch(url).catch (e => { })", Server.EmptyPage)
            );
            StringAssert.Contains("Access-Control-Allow-Origin", messageEvent.Text);
            Assert.AreEqual("error", messageEvent.Type);
        }

        [PlaywrightTest("page-event-console.spec.ts", "should have location for console API calls")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveLocationForConsoleAPICalls()
        {
            await Page.GotoAsync(Server.EmptyPage);
            var messageEvent = await Page.RunAndWaitForConsoleMessageAsync(async () =>
            {
                await Page.GotoAsync(Server.Prefix + "/consolelog.html");
            });
            Assert.AreEqual("yellow", messageEvent.Text);
            Assert.AreEqual("log", messageEvent.Type);
            string location = messageEvent.Location;
        }

        [PlaywrightTest("page-event-console.spec.ts", "should not throw when there are console messages in detached iframes")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowWhenThereAreConsoleMessagesInDetachedIframes()
        {
            await Page.GotoAsync(Server.EmptyPage);
            var (popup, _) = await TaskUtils.WhenAll(
                Page.WaitForPopupAsync(),
                Page.EvaluateAsync<bool>(@"async () =>
                {
                    // 1. Create a popup that Playwright is not connected to.
                    const win = window.open('');
                    window._popup = win;
                    if (window.document.readyState !== 'complete')
                      await new Promise(f => window.addEventListener('load', f));
                    // 2. In this popup, create an iframe that console.logs a message.
                    win.document.body.innerHTML = `<iframe src='/consolelog.html'></iframe>`;
                    const frame = win.document.querySelector('iframe');
                    if (!frame.contentDocument || frame.contentDocument.readyState !== 'complete')
                      await new Promise(f => frame.addEventListener('load', f));
                    // 3. After that, remove the iframe.
                    frame.remove();
                }"));
            // 4. Connect to the popup and make sure it doesn't throw.
            Assert.AreEqual(2, await popup.EvaluateAsync<int>("1 + 1"));
        }
    }
}
