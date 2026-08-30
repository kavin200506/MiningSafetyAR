using System;
using System.Collections.Generic;
using UnityEngine;
using MiningSafetyAR.Data;

namespace MiningSafetyAR.Assessment
{
    public class AssessmentEngine : MonoBehaviour
    {
        public static AssessmentEngine Instance { get; private set; }

        [Header("Assessment Settings")]
        [SerializeField] private float passThresholdPercentage = 70f;

        private List<QuizQuestion> currentQuizQuestions = new List<QuizQuestion>();
        private int currentQuestionIndex = 0;
        private int correctAnswersCount = 0;
        private string activeModuleName = "";

        public event Action<QuizQuestion, int, int> OnQuestionLoaded;
        public event Action<TrainingResult> OnQuizCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void StartQuiz(ModuleType moduleType)
        {
            currentQuizQuestions = LoadQuestionsForModule(moduleType);
            currentQuestionIndex = 0;
            correctAnswersCount = 0;
            activeModuleName = moduleType == ModuleType.FireAndExplosion ? "Fire & Explosion Response" : "Gas Leak & Confined Space Protocol";

            if (currentQuizQuestions.Count > 0)
            {
                LoadNextQuestion();
            }
            else
            {
                Debug.LogWarning("[AssessmentEngine] No quiz questions found for module!");
            }
        }

        public bool SubmitAnswer(int selectedOptionIndex)
        {
            if (currentQuestionIndex < 0 || currentQuestionIndex >= currentQuizQuestions.Count) return false;

            QuizQuestion question = currentQuizQuestions[currentQuestionIndex];
            bool isCorrect = (selectedOptionIndex == question.correctOptionIndex);

            if (isCorrect)
            {
                correctAnswersCount++;
            }

            currentQuestionIndex++;

            if (currentQuestionIndex < currentQuizQuestions.Count)
            {
                LoadNextQuestion();
            }
            else
            {
                CompleteQuiz();
            }

            return isCorrect;
        }

        private void LoadNextQuestion()
        {
            QuizQuestion q = currentQuizQuestions[currentQuestionIndex];
            OnQuestionLoaded?.Invoke(q, currentQuestionIndex + 1, currentQuizQuestions.Count);
        }

        private void CompleteQuiz()
        {
            int total = currentQuizQuestions.Count;
            float percentage = total > 0 ? ((float)correctAnswersCount / total) * 100f : 0f;
            bool passed = percentage >= passThresholdPercentage;

            TrainingResult result = new TrainingResult
            {
                workerId = PlayerPrefs.GetString("WorkerID", "WORKER_001"),
                moduleName = activeModuleName + " (Assessment)",
                score = correctAnswersCount,
                maxScore = total,
                percentage = percentage,
                passed = passed,
                mistakesCount = total - correctAnswersCount,
                completionTimeSeconds = 0f
            };

            if (LocalScoreManager.Instance != null)
            {
                LocalScoreManager.Instance.SaveResult(result);
            }
            // Also save to Firestore via AppDataService
            if (Data.AppDataService.Instance != null)
            {
                Data.AppDataService.Instance.SaveAttempt(activeModuleName, (int)percentage, passed);
            }

            Debug.Log($"[AssessmentEngine] Quiz complete: {correctAnswersCount}/{total} ({percentage}%). Passed: {passed}");
            OnQuizCompleted?.Invoke(result);
        }

        private List<QuizQuestion> LoadQuestionsForModule(ModuleType moduleType)
        {
            List<QuizQuestion> questions = new List<QuizQuestion>();

            if (moduleType == ModuleType.FireAndExplosion)
            {
                questions.Add(new QuizQuestion
                {
                    questionId = "FE_Q1",
                    questionTextTextEN = "What does the 'P' in the P.A.S.S. fire extinguisher technique stand for?",
                    questionTextTextHI = "P.A.S.S. अग्निशामक तकनीक में 'P' का क्या अर्थ है?",
                    questionTextTextSAT = "P.A.S.S. seng serek tekinik re 'P' reyak chhed reyak kana?",
                    optionsEN = new string[] { "Point nozzle", "Pull pin", "Pressure check", "Push handle" },
                    optionsHI = new string[] { "नोजल पॉइंट करें", "पिन खींचें", "प्रेशर चेक करें", "हैंडल दबाएं" },
                    optionsSAT = new string[] { "Nozzle point", "Pin oroy me", "Pressure check", "Handle lin me" },
                    correctOptionIndex = 1
                });

                questions.Add(new QuizQuestion
                {
                    questionId = "FE_Q2",
                    questionTextTextEN = "Where should you aim the fire extinguisher nozzle?",
                    questionTextTextHI = "अग्निशामक के नोजल को कहाँ निशाना बनाना चाहिए?",
                    questionTextTextSAT = "Seng serek nozzle okare aim laga-a?",
                    optionsEN = new string[] { "At top of flames", "At the center of smoke", "At the base of the fire", "At surrounding walls" },
                    optionsHI = new string[] { "लपटों के ऊपर", "धुएं के बीच में", "आग के आधार/जड़ पर", "आसपास की दीवारों पर" },
                    optionsSAT = new string[] { "Lapo chetan re", "Dhoo tala re", "Seng reyak buta re", "Bhith re" },
                    correctOptionIndex = 2
                });
            }
            else
            {
                questions.Add(new QuizQuestion
                {
                    questionId = "GL_Q1",
                    questionTextTextEN = "Which protective equipment is mandatory when entering a toxic gas confined space?",
                    questionTextTextHI = "विषैली गैस वाले बंद स्थान में प्रवेश करते समय कौन सा सुरक्षा उपकरण अनिवार्य है?",
                    questionTextTextSAT = "Bish ghas bhitir bolo joga okatag rukhiye bhabhahar niyati kana?",
                    optionsEN = new string[] { "Cloth Dust Mask", "Self-Contained Breathing Apparatus (SCBA)", "Safety Goggles only", "Ear Plugs" },
                    optionsHI = new string[] { "कपड़े का मास्क", "एससीबीए (SCBA) श्वसन यंत्र", "केवल सुरक्षा चश्मा", "ईयर प्लग" },
                    optionsSAT = new string[] { "Lopo mask", "SCBA (Self-Contained Breathing Apparatus)", "Chasma sumung", "Lutur plug" },
                    correctOptionIndex = 1
                });
            }

            return questions;
        }
    }
}
