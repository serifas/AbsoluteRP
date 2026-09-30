using Dalamud.Interface;

namespace AbsoluteRP.RsUI.Pages;

// Every page the hub can show implements this. Draw fills the body, Icon/Title feed the side nav so we don't repeat ourselves.
public interface IPage
{
    string Id { get; }
    string Title { get; }
    FontAwesomeIcon Icon { get; }
    void Draw();

    // Fired when the hub shows/hides this page, so hosted legacy windows can run their OnOpen/OnClose data fetching.
    void OnSelected() { }
    void OnDeselected() { }
}
