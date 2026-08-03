/**
 * Renders a monochrome SVG as a CSS mask rather than an <img> - the source
 * files are inconsistently colored (some black-fill, some white-fill, some
 * pre-colored orange), so masking lets every icon inherit `currentColor`/
 * `background-color` from the surrounding theme instead of clashing with it.
 */
export function MaskIcon({ src, className, label }: { src: string; className?: string; label?: string }) {
  return (
    <span
      className={`icon-mask ${className ?? ""}`}
      style={{ WebkitMaskImage: `url(${src})`, maskImage: `url(${src})` }}
      role={label ? "img" : undefined}
      aria-label={label}
    />
  );
}
