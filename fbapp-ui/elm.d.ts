declare module '*.elm' {
    export const Elm: {
        Main: {
            init(args: { node: Element }): void
        }
    }
}
