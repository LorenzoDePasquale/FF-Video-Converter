using System.Collections.Generic;
using System.Text;
using System.Linq;


namespace FFVideoConverter
{
    public interface IFilter
    {
        string FilterName { get; }

        string GetFilter();
    }


    class Filtergraph
    {
        /// <summary>
        /// Returns the total number of filters added to this filtergraph
        /// </summary>
        public int Count { get => filters.Values.Sum(x => x.Count); }

        readonly Dictionary<(int inputIndex, int streamIndex), List<IFilter>> filters;


        public Filtergraph()
        {
            filters = new Dictionary<(int inputIndex, int streamIndex), List<IFilter>>();
        }

        public void AddFilter(IFilter filter, int inputIndex, int streamIndex)
        {
            if (filters.ContainsKey((inputIndex, streamIndex)))
            {
                filters[(inputIndex, streamIndex)].Add(filter);
            }
            else
            {
                filters.Add((inputIndex, streamIndex), new List<IFilter>());
                filters[(inputIndex, streamIndex)].Add(filter);
            }
        }

        public void AddFilters(IEnumerable<IFilter> filters, int inputIndex, int streamIndex)
        {
            if (this.filters.ContainsKey((0, streamIndex)))
            {
                this.filters[(0, streamIndex)].AddRange(filters);
            }
            else
            {
                this.filters.Add((inputIndex, streamIndex), new List<IFilter>());
                this.filters[(inputIndex, streamIndex)].AddRange(filters);
            }
        }

        public string GenerateFiltergraph()
        {
            StringBuilder sb = new StringBuilder();

            foreach (var filterGroup in filters)
            {
                sb.Append($"[{filterGroup.Key.inputIndex}:{filterGroup.Key.streamIndex}]");
                sb.Append(GenerateFilterchain(filterGroup.Value));
                sb.Append(';');
            }
            sb.Remove(sb.Length - 1, 1);

            return sb.ToString();
        }

        private string GenerateFilterchain(List<IFilter> filters)
        {
            return string.Join(",", filters.Select(f => f.GetFilter()));
        }
    }
}