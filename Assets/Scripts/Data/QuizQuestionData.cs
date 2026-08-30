using System;

namespace MiningSafetyAR.Data
{
    [Serializable]
    public class QuizQuestionData
    {
        public string id;
        public string moduleId;
        public string textEN;
        public string textHI;
        public string textSAT;
        public string[] optionsEN;
        public string[] optionsHI;
        public string[] optionsSAT;
        public int correctIndex;
        public string competency;
    }
}
