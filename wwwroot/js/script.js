function detectKeyboard() {
    var isKeyboardVisible = window.innerHeight < screen.height * 0.85;
    DotNet.invokeMethodAsync('Manta', 'KeyboardChanged', isKeyboardVisible);
}

function scrollToEnd(id) {
    const element = document.getElementById(id);
    if (element) {
        element.scrollIntoView({
            behavior: "instant",
        })
    }
}

window.ScrollToEnd = scrollToEnd;
window.addEventListener('resize', detectKeyboard);