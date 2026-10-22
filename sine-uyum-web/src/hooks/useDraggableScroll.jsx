import { useRef, useEffect } from 'react';

/**
 * Custom React hook that lets the user scroll an element horizontally by dragging it with the mouse.
 * @returns {React.RefObject} Ref to attach to the draggable element.
 */
export const useDraggableScroll = () => {
    // Ref to the DOM element being dragged.
    const ref = useRef(null);

    useEffect(() => {
        const el = ref.current;
        if (!el) return; // Nothing to do until the element has rendered.

        // Drag state and starting positions
        let isDown = false;
        let startX;
        let scrollLeft;

        // Fired when the mouse button is pressed
        const onMouseDown = (e) => {
            isDown = true;
            el.classList.add('active-drag');
            startX = e.pageX - el.offsetLeft;
            scrollLeft = el.scrollLeft;
        };

        // Fired when the mouse leaves the element
        const onMouseLeave = () => {
            isDown = false;
            el.classList.remove('active-drag');
        };

        // Fired when the mouse button is released
        const onMouseUp = () => {
            isDown = false;
            el.classList.remove('active-drag');
        };

        // Fired when the mouse moves
        const onMouseMove = (e) => {
            if (!isDown) return; // Only while the button is held down
            e.preventDefault(); // Prevent unwanted browser behaviour such as text selection
            const x = e.pageX - el.offsetLeft;
            const walk = (x - startX) * 2; // Multiplier to speed up scrolling
            el.scrollLeft = scrollLeft - walk;
        };

        // Attach the event listeners
        el.addEventListener('mousedown', onMouseDown);
        el.addEventListener('mouseleave', onMouseLeave);
        el.addEventListener('mouseup', onMouseUp);
        el.addEventListener('mousemove', onMouseMove);

        // Remove the event listeners on unmount
        return () => {
            el.removeEventListener('mousedown', onMouseDown);
            el.removeEventListener('mouseleave', onMouseLeave);
            el.removeEventListener('mouseup', onMouseUp);
            el.removeEventListener('mousemove', onMouseMove);
        };
    }, []); // Run only on the first render

    return ref;
};
