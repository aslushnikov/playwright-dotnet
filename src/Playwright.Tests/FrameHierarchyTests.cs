using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Microsoft.Playwright.Tests
{
    [Parallelizable(ParallelScope.Self)]
    public class FrameHierarchyTests : PageTestEx
    {
        [PlaywrightTest("frame-hierarchy.spec.ts", "should handle nested frames")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHandleNestedFrames()
        {
            await Page.GotoAsync(Server.Prefix + "/frames/nested-frames.html");
            Assert.AreEqual(TestConstants.NestedFramesDumpResult, FrameUtils.DumpFrames(Page.MainFrame));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should send events when frames are manipulated dynamically")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendEventsWhenFramesAreManipulatedDynamically()
        {
            await Page.GotoAsync(Server.EmptyPage);
            // validate frameattached events
            var attachedFrames = new List<IFrame>();
            Page.FrameAttached += (_, e) => attachedFrames.Add(e);
            await FrameUtils.AttachFrameAsync(Page, "frame1", "./assets/frame.html");
            Assert.That(attachedFrames, Has.Count.EqualTo(1));
            StringAssert.Contains("/assets/frame.html", attachedFrames[0].Url);

            // validate framenavigated events
            var navigatedFrames = new List<IFrame>();
            Page.FrameNavigated += (_, e) => navigatedFrames.Add(e);
            await Page.EvaluateAsync(@"() => {
                const frame = document.getElementById('frame1');
                frame.src = './empty.html';
                return new Promise(x => frame.onload = x);
            }");
            Assert.That(navigatedFrames, Has.Count.EqualTo(1));
            Assert.AreEqual(Server.EmptyPage, navigatedFrames[0].Url);

            // validate framedetached events
            var detachedFrames = new List<IFrame>();
            Page.FrameDetached += (_, e) => detachedFrames.Add(e);
            await FrameUtils.DetachFrameAsync(Page, "frame1");
            Assert.That(detachedFrames, Has.Count.EqualTo(1));
            Assert.True(detachedFrames[0].IsDetached);
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", @"should send ""framenavigated"" when navigating on anchor URLs")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendFrameNavigatedWhenNavigatingOnAnchorURLs()
        {
            await Page.GotoAsync(Server.EmptyPage);

            await TaskUtils.WhenAll(
                Page.WaitForNavigationAsync(),
                Page.GotoAsync(Server.EmptyPage + "#foo"));

            Assert.AreEqual(Server.EmptyPage + "#foo", Page.Url);
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should persist mainFrame on cross-process navigation")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPersistMainFrameOnCrossProcessNavigation()
        {
            await Page.GotoAsync(Server.EmptyPage);
            var mainFrame = Page.MainFrame;
            await Page.GotoAsync(Server.CrossProcessPrefix + "/empty.html");
            Assert.AreEqual(mainFrame, Page.MainFrame);
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should not send attach/detach events for main frame")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotSendAttachDetachEventsForMainFrame()
        {
            bool hasEvents = false;
            Page.FrameAttached += (_, _) => hasEvents = true;
            Page.FrameDetached += (_, _) => hasEvents = true;
            await Page.GotoAsync(Server.EmptyPage);
            Assert.False(hasEvents);
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should detach child frames on navigation")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDetachChildFramesOnNavigation()
        {
            var attachedFrames = new List<IFrame>();
            var detachedFrames = new List<IFrame>();
            var navigatedFrames = new List<IFrame>();
            Page.FrameAttached += (_, e) => attachedFrames.Add(e);
            Page.FrameDetached += (_, e) => detachedFrames.Add(e);
            Page.FrameNavigated += (_, e) => navigatedFrames.Add(e);
            await Page.GotoAsync(Server.Prefix + "/frames/nested-frames.html");
            Assert.AreEqual(4, attachedFrames.Count);
            Assert.IsEmpty(detachedFrames);
            Assert.AreEqual(5, navigatedFrames.Count);

            attachedFrames.Clear();
            detachedFrames.Clear();
            navigatedFrames.Clear();
            await Page.GotoAsync(Server.EmptyPage);
            Assert.IsEmpty(attachedFrames);
            Assert.AreEqual(4, detachedFrames.Count);
            Assert.That(navigatedFrames, Has.Count.EqualTo(1));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should support framesets")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportFramesets()
        {
            var attachedFrames = new List<IFrame>();
            var detachedFrames = new List<IFrame>();
            var navigatedFrames = new List<IFrame>();
            Page.FrameAttached += (_, e) => attachedFrames.Add(e);
            Page.FrameDetached += (_, e) => detachedFrames.Add(e);
            Page.FrameNavigated += (_, e) => navigatedFrames.Add(e);
            await Page.GotoAsync(Server.Prefix + "/frames/frameset.html");
            Assert.AreEqual(4, attachedFrames.Count);
            Assert.IsEmpty(detachedFrames);
            Assert.AreEqual(5, navigatedFrames.Count);

            attachedFrames.Clear();
            detachedFrames.Clear();
            navigatedFrames.Clear();
            await Page.GotoAsync(Server.EmptyPage);
            Assert.IsEmpty(attachedFrames);
            Assert.AreEqual(4, detachedFrames.Count);
            Assert.That(navigatedFrames, Has.Count.EqualTo(1));
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should report frame from-inside shadow DOM")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportFrameFromInsideShadowDOM()
        {
            await Page.GotoAsync(Server.Prefix + "/shadow.html");
            await Page.EvaluateAsync(@"async url => {
                const frame = document.createElement('iframe');
                frame.src = url;
                document.body.shadowRoot.appendChild(frame);
                await new Promise(x => frame.onload = x);
            }", Server.EmptyPage);
            Assert.AreEqual(2, Page.Frames.Count);
            Assert.AreEqual(Server.EmptyPage, Page.Frames.ElementAt(1).Url);
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should report frame.name()")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportFrameName()
        {
            await FrameUtils.AttachFrameAsync(Page, "theFrameId", Server.EmptyPage);
            await Page.EvaluateAsync(@"url => {
                const frame = document.createElement('iframe');
                frame.name = 'theFrameName';
                frame.src = url;
                document.body.appendChild(frame);
                return new Promise(x => frame.onload = x);
            }", Server.EmptyPage);
            Assert.IsEmpty(Page.Frames.First().Name);
            Assert.AreEqual("theFrameId", Page.Frames.ElementAt(1).Name);
            Assert.AreEqual("theFrameName", Page.Frames.ElementAt(2).Name);
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should report frame.parent()")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportFrameParent()
        {
            await FrameUtils.AttachFrameAsync(Page, "frame1", Server.EmptyPage);
            await FrameUtils.AttachFrameAsync(Page, "frame2", Server.EmptyPage);
            Assert.Null(Page.Frames.First().ParentFrame);
            Assert.AreEqual(Page.MainFrame, Page.Frames.ElementAt(1).ParentFrame);
            Assert.AreEqual(Page.MainFrame, Page.Frames.ElementAt(2).ParentFrame);
        }

        [PlaywrightTest("frame-hierarchy.spec.ts", "should report different frame instance when frame re-attaches")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportDifferentFrameInstanceWhenFrameReAttaches()
        {
            var frame1 = await FrameUtils.AttachFrameAsync(Page, "frame1", Server.EmptyPage);
            await Page.EvaluateAsync(@"() => {
                window.frame = document.querySelector('#frame1');
                window.frame.remove();
            }");
            Assert.True(frame1.IsDetached);

            var frameEvent = new TaskCompletionSource<IFrame>();
            Page.FrameNavigated += (_, frame) => frameEvent.TrySetResult(frame);

            var (frame2, _) = await TaskUtils.WhenAll(
              frameEvent.Task,
              Page.EvaluateAsync("() => document.body.appendChild(window.frame)")
            );

            Assert.False(frame2.IsDetached);
            Assert.That(frame1, Is.Not.EqualTo(frame2));
        }
    }
}
