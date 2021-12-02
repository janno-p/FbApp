declare module '*.elm' {
    interface IElmMain {
        ports: {
            authenticated: {
                send(args: any): void
            }
        }
    }

    export const Elm: {
        Main: {
            init(args: { node: Element }): IElmMain
        }
    }
}
