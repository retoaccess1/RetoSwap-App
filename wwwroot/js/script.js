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

function getSizeOfElement(element) {
    if (!element)
        return null;

    const rect = element.getBoundingClientRect();
    return { width: rect.width, height: rect.height };
}

window.GetSizeOfElement = getSizeOfElement;
window.ScrollToEnd = scrollToEnd;
window.addEventListener('resize', detectKeyboard);