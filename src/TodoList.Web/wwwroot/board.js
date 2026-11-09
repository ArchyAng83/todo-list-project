// HTML5 drag and drop requires a data transfer payload in some browsers before
// a drag can start. Blazor owns the drop handlers; this only seeds the payload.
document.addEventListener('dragstart', function (event) {
    var card = event.target && event.target.closest
        ? event.target.closest('.board-card')
        : null;

    if (!card || !event.dataTransfer) {
        return;
    }

    event.dataTransfer.setData('text/plain', card.getAttribute('data-task-id') || 'task');
    event.dataTransfer.effectAllowed = 'move';
});
