import { useRef, useEffect } from 'react';

/**
 * Bir DOM elemanına fare ile sürükleyerek yatay kaydırma özelliği ekleyen özel bir React hook'u.
 * @returns {React.RefObject} Sürüklenmesi istenen elemana atanacak olan ref nesnesi.
 */
export const useDraggableScroll = () => {
    // Sürüklenen DOM elemanına referans tutmak için useRef kullanıyoruz.
    const ref = useRef(null);

    useEffect(() => {
        const el = ref.current;
        if (!el) return; // Eleman henüz render edilmediyse bir şey yapma.

        // Sürükleme durumu ve başlangıç pozisyonlarını tutacak değişkenler
        let isDown = false;
        let startX;
        let scrollLeft;

        // Fareye tıklandığında tetiklenir
        const onMouseDown = (e) => {
            isDown = true;
            el.classList.add('active-drag');
            startX = e.pageX - el.offsetLeft;
            scrollLeft = el.scrollLeft;
        };

        // Fare, elemanın dışına çıktığında tetiklenir
        const onMouseLeave = () => {
            isDown = false;
            el.classList.remove('active-drag');
        };

        // Fare tuşu bırakıldığında tetiklenir
        const onMouseUp = () => {
            isDown = false;
            el.classList.remove('active-drag');
        };

        // Fare hareket ettiğinde tetiklenir
        const onMouseMove = (e) => {
            if (!isDown) return; // Sadece fareye basılıyken çalış
            e.preventDefault(); // Metin seçimi gibi istenmeyen tarayıcı davranışlarını engelle
            const x = e.pageX - el.offsetLeft;
            const walk = (x - startX) * 2; // Kaydırma hızını artırmak için bir çarpan
            el.scrollLeft = scrollLeft - walk;
        };

        // Event listener'ları elemana ekle
        el.addEventListener('mousedown', onMouseDown);
        el.addEventListener('mouseleave', onMouseLeave);
        el.addEventListener('mouseup', onMouseUp);
        el.addEventListener('mousemove', onMouseMove);

        // Component kaldırıldığında (unmount) event listener'ları temizle
        return () => {
            el.removeEventListener('mousedown', onMouseDown);
            el.removeEventListener('mouseleave', onMouseLeave);
            el.removeEventListener('mouseup', onMouseUp);
            el.removeEventListener('mousemove', onMouseMove);
        };
    }, []); // Bu useEffect'in sadece component ilk render edildiğinde çalışmasını sağla

    return ref; // Dışarıya ref nesnesini döndür
};
