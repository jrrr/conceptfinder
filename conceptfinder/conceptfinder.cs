using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace conceptfinder
{
	class Server
	{
		// path to a list of the words in the concept terms
		private static string conceptDictPath = "meddict";

		// path to a general list of words with frequency counts, as used by SymSpell
		private static string generalDictPath = "frequency_dictionary_en_82_765.txt";

		static void Main(string[] args)
		{
			ConceptFinder conceptFinder = new ConceptFinder(conceptDictPath, generalDictPath);
			
			Console.InputEncoding = new UTF8Encoding();
			Console.OutputEncoding = new UTF8Encoding();
			TextReader reader = Console.In;
			TextWriter writer = Console.Out;

			string line;
			while ((line = reader.ReadLine()) != null)
			{
				if (line.Trim().Length == 0)
				{
					continue; // ignore empty lines
				}
				string[] parts = line.Trim().Split(' ');

				string method = parts[0];
				if (method != "x" && method != "e")
				{
					Console.WriteLine("invalid method: {0}", line);
					break;
				}

				int num_sentences;
				if (!Int32.TryParse(parts[1], out num_sentences) || num_sentences < 0)
				{
					Console.WriteLine("invalid number of sentences: {0}", line);
					break;
				}

				string[] sentences = new string[num_sentences];
				for (int i = 0; i < num_sentences; i++)
				{
					sentences[i] = reader.ReadLine().Trim();
				}

				List<string> output;
				if (method == "x")
				{
					output = conceptFinder.ExtractConcepts(sentences);
				}
				else
				{
					output = conceptFinder.EncodeConcepts(sentences);
				}

				writer.Write(output.Count + "\n");
				if (output.Count > 0)
				{
					writer.Write(String.Join("\n", output) + "\n");
				}
				writer.Flush();
			}
		}
	}

    class ConceptFinder
    {
		private TermTreeNode termTree;
		private SymSpell symSpell;

        public ConceptFinder(string conceptDictPath, string generalDictPath)
        {
			termTree = new TermTreeNode();
			HashSet<string> termWords = loadTerms(conceptDictPath);
			symSpell = new SymSpell(82765 + termWords.Count, 2, 7);
			foreach (string word in termWords)
			{
				symSpell.CreateDictionaryEntry(word, 1);
			}
			if (generalDictPath != null && generalDictPath != "" &&
				!symSpell.LoadDictionary(generalDictPath, 1, 0))
			{
				throw new FileNotFoundException(generalDictPath);
			}
		}

		public List<string> ExtractConcepts(string[] sentences)
		{
			List<string> concepts = new List<string>();
			foreach (string sentence in sentences)
			{
				// first let SymSpell correct the spelling
				string[] words = symSpell.LookupCompound(sentence.Trim())[0].term
						.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				// then match the words to the concept terms
				List<ConceptMatch> matches = findConcepts(words);
				foreach (ConceptMatch match in matches)
				{
					concepts.Add(match.Cui + " " + match.Length);
				}
			}
			return concepts;
		}

		public List<string> EncodeConcepts(string[] sentences)
		{
			List<string> encodedLines = new List<string>(sentences.Length);
			foreach (string sentence in sentences)
			{
				// first let SymSpell correct the spelling
				string[] words = symSpell.LookupCompound(sentence.Trim())[0].term
						.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				// then match the words to the concept terms
				List<ConceptMatch> matches = findConcepts(words);
				encodedLines.Add(replaceConceptsWithCuis(words, matches));
			}
			return encodedLines;
		}

		private HashSet<string> loadTerms(string path)
		{
			HashSet<string> uniqueWords = new HashSet<string>();
			foreach (string line in File.ReadLines(path))
			{
				string[] parts = line.Split(' ', 3);
				string cui = parts[0];
				string term = parts[2];
				Queue<string[]> parsedTerm = parseTerm(term);
				foreach (string[] words in parsedTerm)
				{
					foreach (string word in words)
					{
						uniqueWords.Add(word);
					}
				}
				termTree.AddTerm(parsedTerm, cui);
			}
			return uniqueWords;
		}

		private Queue<string[]> parseTerm(string term)
		{
			Queue<string[]> parsedTerm = new Queue<string[]>();

			string[] wordSets = term.Split(' ');
			foreach (string wordSet in wordSets)
			{
				parsedTerm.Enqueue(wordSet.Split(','));
			}
			return parsedTerm;
		}

		private List<ConceptMatch> findConcepts(string[] words)
		{
			List<ConceptMatch> matches = new List<ConceptMatch>();
			int index = 0;
			while (index < words.Length)
			{
				(HashSet<string> cuis, int length) = matchConcept(words, index);
				if (length == 0)
				{
					index += 1; // no matching term
				}
				else
				{
					foreach (string cui in cuis)
					{
						matches.Add(new ConceptMatch(cui, index, length));
					}
					index += length;
				}
			}
			return matches;
		}

		private (HashSet<string>, int) matchConcept(string[] words, int index)
		{
			int origIndex = index;
			int bestMatchLength = 0;
			HashSet<string> bestMatchCuis = new HashSet<string>();

			TermTreeNode tt = termTree;
			do
			{
				tt = tt.FollowWord(words[index++]);
				if (tt != null && tt.GetCuis().Count > 0)
				{
					bestMatchLength = index - origIndex;
					bestMatchCuis = tt.GetCuis();
				}
			} while (tt != null && index < words.Length);
			return (bestMatchCuis, bestMatchLength);
		}

		private string replaceConceptsWithCuis(string[] words, List<ConceptMatch> concepts)
		{
			List<string> newWords = new List<string>(words.Length);
			int iConcept = 0;
			int iWord = 0;
			while (iWord < words.Length)
			{
				if (iConcept < concepts.Count && iWord == concepts[iConcept].Index)
				{
					newWords.Add(concepts[iConcept].Cui);
					iWord += concepts[iConcept].Length;
					iConcept += 1;
				}
				else
				{
					newWords.Add(words[iWord++]);
				}
			}
			return String.Join(" ", newWords);
		}
    }

	class ConceptMatch
	{
		public string Cui;
		public int Index;
		public int Length;

		public ConceptMatch(string cui, int index, int length)
		{
			this.Cui = cui;
			this.Index = index;
			this.Length = length;
		}
	}

	class TermTreeNode
	{
		private Dictionary<string,TermTreeNode> words = new Dictionary<string,TermTreeNode>();
		private HashSet<string> cuis = new HashSet<string>();

		public void AddTerm(Queue<string[]> termWords, string cui)
		{
			if (termWords.Count == 0)
			{
				AddCui(cui);
				return;
			}

			string[] curWords = termWords.Dequeue();
			if (FollowWord(curWords[0]) == null)
			{
				// if one word form is missing all are missing
				TermTreeNode newNode = new TermTreeNode();
				foreach (string word in curWords)
				{
					words.Add(word, newNode);
				}
			}
			FollowWord(curWords[0]).AddTerm(termWords, cui);
		}

		public TermTreeNode FollowWord(string word)
		{
			TermTreeNode nextNode;
			if (words.TryGetValue(word, out nextNode))
			{
				return nextNode;
			}
			else
			{
				return null;
			}
		}

		public void AddCui(string cui)
		{
			cuis.Add(cui);
		}

		public HashSet<string> GetCuis()
		{
			return cuis;
		}

		public bool IsEndpoint()
		{
			return cuis.Count > 0;
		}
	}
}
