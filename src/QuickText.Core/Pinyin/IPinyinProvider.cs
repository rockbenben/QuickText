namespace QuickText.Core.Pinyin;

public interface IPinyinProvider
{
    string GetFullPinyin(string text);
    string GetInitials(string text);
}
