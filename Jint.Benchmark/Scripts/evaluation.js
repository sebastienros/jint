var o = {};
o.Foo = 'bar';
o.Baz = 42.0001;
o.Blah = o.Foo + o.Baz;

if(o.Blah != 'bar42.0001') throw TypeError;

function fib(n){
    if(n<2) {
        return n;
    }

    return fib(n-1) + fib(n-2);
}

if(fib(3) != 2) throw TypeError;
