using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.SpeechRecognition;

namespace AssistantItemFinder.Content
{
    /// <summary>
    /// Responsible for managing the SpeechRecognizer and broadcasting recognized phrases when detected 
    /// </summary>
    internal class SpeechManager
    {
        /// <summary>
        /// Called when a phrase is recognized 
        /// </summary>
        /// <param name="status"></param>
        /// <param name="text"></param>
        /// <param name="rulePath"></param>
        /// <param name="semanticInterpretation"></param>
        public delegate void PhraseRecognized(int status, string text, string tag);

        public event PhraseRecognized OnPhraseRecognized = delegate { };

        #region properties and variables 

        string[] findVariants = { "find", "where is", "locate" };

        string[] rememberVariants = { "remember", "save", "tag" };

        string[] rememberTags = { "hat", "keys", "wallet", "umbrella", "coat", "hammer", "jacket", "shoes" }; 

        HashSet<string> tags = new HashSet<string>(); 

        SpeechRecognizer speechRecognizer;

        #endregion 

        public SpeechManager()
        {
            
        }

        #region factory/(de-)initilisation methods 

        /// <summary>
        /// Create and start the SpeechManager 
        /// </summary>
        /// <returns></returns>
        public static async Task<SpeechManager> CreateAndStartAsync()
        {
            var speechManager = new SpeechManager();

            try
            {
                await speechManager.Start();
                return speechManager;
            } 
            catch
            {
                return null; 
            }                       
        }

        List<SpeechRecognitionListConstraint> GetConstraints()
        {
            var constraints = new List<SpeechRecognitionListConstraint>();

            var utterances = new List<string>();

            foreach (var tag in rememberTags)
            {
                utterances.Clear();

                foreach (var rememberVariant in rememberVariants)
                {
                    utterances.Add($"{rememberVariant.ToLower()} {tag.ToLower()}");
                }

                if (utterances.Count > 0)
                {
                    var rememberTag = $"remember_{tag.ToLower()}";
                    constraints.Add(new SpeechRecognitionListConstraint(utterances, rememberTag));
                }
            }

            foreach (var tag in tags)
            {
                utterances.Clear();

                foreach (var findVariant in findVariants)
                {
                    utterances.Add($"{findVariant.ToLower()} {tag.ToLower()}");
                }

                if (utterances.Count > 0)
                {
                    var findTag = $"find_{tag.ToLower()}";
                    constraints.Add(new SpeechRecognitionListConstraint(utterances, findTag));
                }
            }

            return constraints;
        }

        /// <summary>
        /// Add tags to search for 
        /// </summary>
        /// <param name="itemTags"></param>
        public async void AddTagsAsync(params string[] itemTags)
        {
            foreach (var itemTag in itemTags)
            {
                tags.Add(itemTag);
            }

            await Start();
        }

        /// <summary>
        /// Creates, initilises, and starts the SpeechRecognizer. Is expecting a grammar file named GrammarFile
        /// </summary>
        /// <returns></returns>
        public async Task Start()
        {
            Stop();            

            speechRecognizer = new SpeechRecognizer();

            // Provide feedback to the user about the state of the recognizer. This can be used to provide
            // visual feedback to help the user understand whether they're being heard.
            speechRecognizer.StateChanged += SpeechRecognizer_StateChanged;

            var constraints = GetConstraints(); 

            foreach(var constraint in constraints)
            {
                speechRecognizer.Constraints.Add(constraint);
            }            

            SpeechRecognitionCompilationResult compilationResult = await speechRecognizer.CompileConstraintsAsync();

            if (compilationResult.Status != SpeechRecognitionResultStatus.Success)
            {
                Debug.WriteLine("Unable to compile grammar.");
                throw new Exception("Unable to compile grammar."); 
            }
            else
            {
                // Set EndSilenceTimeout to give users more time to complete speaking a phrase.
                speechRecognizer.Timeouts.EndSilenceTimeout = TimeSpan.FromSeconds(1.2);

                // Handle continuous recognition events. Completed fires when various error states occur. ResultGenerated fires when
                // some recognized phrases occur, or the garbage rule is hit.
                speechRecognizer.ContinuousRecognitionSession.Completed += ContinuousRecognitionSession_Completed;
                speechRecognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
            }

            await speechRecognizer.ContinuousRecognitionSession.StartAsync();
        }

        public void Stop()
        {
            if (speechRecognizer != null)
            {
                // cleanup prior to re-initializing this scenario.
                speechRecognizer.ContinuousRecognitionSession.Completed -= ContinuousRecognitionSession_Completed;
                speechRecognizer.ContinuousRecognitionSession.ResultGenerated -= ContinuousRecognitionSession_ResultGenerated;
                speechRecognizer.StateChanged -= SpeechRecognizer_StateChanged;

                speechRecognizer.Dispose();
                speechRecognizer = null;
            }
        }

        #endregion

        #region SpeechRecognizer handlers 

        /// <summary>
        /// Provide feedback to the user based on whether the recognizer is receiving their voice input.
        /// </summary>
        /// <param name="sender">The recognizer that is currently running.</param>
        /// <param name="args">The current state of the recognizer.</param>
        private void SpeechRecognizer_StateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            Debug.WriteLine($"SpeechRecognizer_StateChanged {args.State.ToString()}");
        }

        /// <summary>
        /// Handle events fired when the session ends, either from a call to
        /// CancelAsync() or StopAsync(), or an error condition, such as the 
        /// microphone becoming unavailable or some transient issues occuring.
        /// </summary>
        /// <param name="sender">The continuous recognition session</param>
        /// <param name="args">The state of the recognizer</param>
        private void ContinuousRecognitionSession_Completed(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
        {
            
        }

        /// <summary>
        /// Handle events fired when a result is generated. This may include a garbage rule that fires when general room noise
        /// or side-talk is captured (this will have a confidence of Rejected typically, but may occasionally match a rule with
        /// low confidence).
        /// </summary>
        /// <param name="sender">The Recognition session that generated this result</param>
        /// <param name="args">Details about the recognized speech</param>
        private void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender,
            SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            var text = string.Empty;
            var tag = string.Empty;

            if (args.Result != null)
            {
                text = args.Result.Text;
            }

            if (args.Result.Constraint != null)
            {
                tag = args.Result.Constraint.Tag;
            }


            // Developers may decide to use per-phrase confidence levels in order to tune the behavior of their 
            // grammar based on testing.
            if (!string.IsNullOrEmpty(tag) &&
                (args.Result.Confidence == SpeechRecognitionConfidence.Medium || args.Result.Confidence == SpeechRecognitionConfidence.High))
            {

                OnPhraseRecognized(1, text, tag);
            }
            else
            {
                OnPhraseRecognized(-1, text, tag);
            }
        }

        #endregion 
    }
}
