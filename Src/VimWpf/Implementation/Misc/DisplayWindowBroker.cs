using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;

namespace Vim.UI.Wpf.Implementation.Misc
{
    /// <summary>
    /// Standard implementation of the IDisplayWindowBroker interface.  This acts as a single
    /// interface for the various completion window possibilities
    /// </summary>
    internal sealed class DisplayWindowBroker : IDisplayWindowBroker
    {
        private readonly ITextView _textView;
        private readonly ICompletionBroker _completionBroker;
        private readonly IAsyncCompletionBroker _asyncCompletionBroker;
        private readonly ISignatureHelpBroker _signatureHelpBroker;
        private readonly IAsyncQuickInfoBroker _quickInfoBroker;
        private readonly JoinableTaskFactory _joinableTaskFactory;

        internal DisplayWindowBroker(
            ITextView textView,
            ICompletionBroker completionBroker,
            IAsyncCompletionBroker asyncCompletionBroker,
            ISignatureHelpBroker signatureHelpBroker,
            IAsyncQuickInfoBroker quickInfoBroker,
            JoinableTaskFactory joinableTaskFactory)
        {
            _textView = textView;
            _completionBroker = completionBroker;
            _asyncCompletionBroker = asyncCompletionBroker;
            _signatureHelpBroker = signatureHelpBroker;
            _quickInfoBroker = quickInfoBroker;
            _joinableTaskFactory = joinableTaskFactory;
        }

        #region IDisplayWindowBroker

        bool IDisplayWindowBroker.IsCompletionActive
        {
            get
            {
                // Check both legacy and async completion brokers.  AI completion systems
                // (IntelliCode, Roslyn, GitHub Copilot completion dropdown) use the newer
                // IAsyncCompletionBroker exclusively, so the legacy ICompletionBroker alone
                // is not sufficient to detect whether a completion session is active.
                return _completionBroker.IsCompletionActive(_textView) ||
                       _asyncCompletionBroker.GetSession(_textView) != null;
            }
        }

        bool IDisplayWindowBroker.IsQuickInfoActive
        {
            get { return _quickInfoBroker.IsQuickInfoActive(_textView); }
        }

        bool IDisplayWindowBroker.IsSignatureHelpActive
        {
            get { return _signatureHelpBroker.IsSignatureHelpActive(_textView); }
        }

        ITextView IDisplayWindowBroker.TextView
        {
            get { return _textView; }
        }

        void IDisplayWindowBroker.DismissDisplayWindows()
        {
            if (_completionBroker.IsCompletionActive(_textView))
            {
                _completionBroker.DismissAllSessions(_textView);
            }

            _asyncCompletionBroker.GetSession(_textView)?.Dismiss();

            if (_signatureHelpBroker.IsSignatureHelpActive(_textView))
            {
                _signatureHelpBroker.DismissAllSessions(_textView);
            }

            if (_quickInfoBroker.IsQuickInfoActive(_textView))
            {
                var session = _quickInfoBroker.GetSession(_textView);
                if (session is object)
                {
                    _joinableTaskFactory.Run(() => session.DismissAsync());
                }
            }
        }

#endregion
    }

    [Export(typeof(IDisplayWindowBrokerFactoryService))]
    internal sealed class DisplayWindowBrokerFactoryService : IDisplayWindowBrokerFactoryService
    {
        private static readonly object s_key = new object();

        private readonly ICompletionBroker _completionBroker;
        private readonly IAsyncCompletionBroker _asyncCompletionBroker;
        private readonly ISignatureHelpBroker _signatureHelpBroker;
        private readonly IAsyncQuickInfoBroker _quickInfoBroker;
        private readonly JoinableTaskContext _joinableTaskContext;

        [ImportingConstructor]
        internal DisplayWindowBrokerFactoryService(
            ICompletionBroker completionBroker,
            IAsyncCompletionBroker asyncCompletionBroker,
            ISignatureHelpBroker signatureHelpBroker,
            IAsyncQuickInfoBroker quickInfoBroker,
            JoinableTaskContext joinableTaskContext)
        {
            _completionBroker = completionBroker;
            _asyncCompletionBroker = asyncCompletionBroker;
            _signatureHelpBroker = signatureHelpBroker;
            _quickInfoBroker = quickInfoBroker;
            _joinableTaskContext = joinableTaskContext;
        }

        IDisplayWindowBroker IDisplayWindowBrokerFactoryService.GetDisplayWindowBroker(ITextView textView)
        {
            return textView.Properties.GetOrCreateSingletonProperty(
                s_key,
                () => new DisplayWindowBroker(
                        textView,
                        _completionBroker,
                        _asyncCompletionBroker,
                        _signatureHelpBroker,
                        _quickInfoBroker,
                        _joinableTaskContext.Factory));
        }
    }
}
