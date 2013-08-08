/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-1.js
 * @description Object.isFrozen - inherited data property is not considered into the for each loop
 */


function testcase() {

        var proto = {};
        Object.defineProperty(proto, "Father", {
            value: 10,
            writable: false,
            configurable: true
        });

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();

        Object.preventExtensions(child);

        return Object.isFrozen(child);

    }
runTestCase(testcase);
