using Antlr4.Runtime.Misc;

namespace DB_COURSE_WORK;

public interface IGetChildrens
{
    public Task<List<int>> GetChild(int id, int level);
    public Task Tag(int id, List<int> tags);
}

public class TagAlg
{
    private IGetChildrens _getChildren;
    private Stack<Tuple<int, int, List<int>>> _st = new Stack<Tuple<int, int, List<int>>>(); //id, level, labels

    public TagAlg(IGetChildrens getChildrens, List<int> lst, int level)
    {
        _getChildren = getChildrens;
        lst.ForEach(x => _st.Push(new Tuple<int, int, List<int>>(x, level, new List<int>())));
    }

    public async Task DoTag()
    {
        while (_st.Count != 0)
        {
            var current = _st.Pop();

            var labels = current.Item3;

            if (current.Item2 > 0)
            {
                labels.Add(current.Item1);
                var childrens = await _getChildren.GetChild(current.Item1, current.Item2);

                childrens.ForEach(ch =>
                    _st.Push(new Tuple<int, int, List<int>>(ch, current.Item2 - 1, labels))
                );
            }
            else
            {
                await _getChildren.Tag(current.Item1, labels);
            }
        }
    }
}