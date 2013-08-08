/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.13/11.13.2/11.13.2-32-s.js
 * @description Strict Mode - TypeError is thrown if The LeftHandSide of a Compound Assignment operator(^=) is a reference to a data property with the attribute value {[[Writable]]:false}
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var obj = {};
        Object.defineProperty(obj, "prop", {
            value: 10,
            writable: false,
            enumerable: true,
            configurable: true
        });

        try {
            obj.prop ^= 20;
            return false;
        } catch (e) {
            return e instanceof TypeError && obj.prop === 10;
        }
    }
runTestCase(testcase);
