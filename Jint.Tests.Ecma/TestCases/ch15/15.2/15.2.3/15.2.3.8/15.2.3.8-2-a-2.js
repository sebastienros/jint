/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-2.js
 * @description Object.seal - 'P' is own data property that overrides an inherited data property
 */


function testcase() {
        var proto = { foo: 0 };

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;

        var child = new ConstructFun();
        Object.defineProperty(child, "foo", {
            value: 10,
            configurable: true
        });
        var preCheck = Object.isExtensible(child);
        Object.seal(child);

        delete child.foo;
        return preCheck && child.foo === 10;
    }
runTestCase(testcase);
