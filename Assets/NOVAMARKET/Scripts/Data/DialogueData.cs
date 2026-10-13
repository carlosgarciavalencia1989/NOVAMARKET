using System;
using System.Collections.Generic;
using NovaMarket.Simulation.Levels;
using UnityEngine;

namespace NovaMarket.Data
{
    /// <summary>
    /// What one character says in one level (Data/Dialogues/). Each line has a key the level asks for
    /// ("intro", "exito"…), an order among the lines with the same key (an intro alternates characters),
    /// a mood and the text with {placeholders} the level fills in ({codes}, {missing}…). Leave role and
    /// sector empty for lines that apply to all; a file for a given role or sector replaces them.
    /// </summary>
    [CreateAssetMenu(menuName = MenuRoot + "Dialogue", fileName = "Dialogue_")]
    public sealed class DialogueData : NovaDataAsset
    {
        /// <summary>One line of the file, as edited in the Inspector.</summary>
        [Serializable]
        public sealed class Line
        {
            [Tooltip("Which moment of the level: intro, exito, aviso-proyeccion, pista-<tema>…")]
            public string key;
            [Tooltip("Position among the lines with the same key (all characters of the level).")]
            public int order = 1;
            public DialogueMood mood;
            [TextArea(2, 6)] public string text;
        }

        [SerializeField] private CharacterData character;
        [SerializeField, Tooltip("Level id: 1.1, 1.2…")] private string levelId;
        [SerializeField, Tooltip("Empty = every role.")] private string roleId;
        [SerializeField, Tooltip("Empty = every sector.")] private string sectorId;
        [SerializeField] private Line[] lines = new Line[0];

        public CharacterData Character => character;
        public string LevelId => levelId;
        public IReadOnlyList<Line> Lines => lines;

        public IEnumerable<DialogueEntry> ToEntries()
        {
            if (character == null) yield break;
            foreach (Line line in lines)
                if (line != null && !string.IsNullOrEmpty(line.key))
                    yield return new DialogueEntry(levelId, character.Id, line.key, line.order, line.mood, line.text, roleId, sectorId);
        }
    }
}
