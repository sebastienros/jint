/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-6.js
 * @description Object.seal - 'P' is own accessor property that overrides an inherited accessor property
 */


function testcase() {
        var proto = {};

        Object.defineProperty(proto, "foo", {
            get: function () {
                return 0;
            },
            configurable: true
        });

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;

        var child = new ConstructFun();

        Object.defineProperty(child, "foo", {
            get: function () {
                return 10;
            },
            configurable: true
        });
        var preCheck = Object.isExtensible(child);
        Object.seal(child);

        delete child.foo;
        return preCheck && child.foo === 10;
    }
runTestCase(testcase);
