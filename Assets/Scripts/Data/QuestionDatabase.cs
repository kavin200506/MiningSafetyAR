using System.Collections.Generic;
using UnityEngine;

namespace MiningSafetyAR.Data
{
    [CreateAssetMenu(fileName = "QuestionDatabase", menuName = "MiningSafetyAR/Question Database")]
    public class QuestionDatabase : ScriptableObject
    {
        public List<QuizQuestionData> questions = new List<QuizQuestionData>();

        public List<QuizQuestionData> GetForModule(string moduleId) => questions.FindAll(q => q.moduleId == moduleId);
        public QuizQuestionData GetById(string id) => questions.Find(q => q.id == id);
    }
}
