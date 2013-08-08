/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.13/11.13.2/11.13.2-44-s.js
 * @description Strict Mode - TypeError is thrown if The LeftHandSide of a Compound Assignment operator(|=) is a reference of to an accessor property with the attribute value {[[Set]]:undefined}
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var obj = {};
        Object.defineProperty(obj, "prop", {
            get: function () {
                return 11;
            },
            set: undefined,
            enumerable: true,
            configurable: true
        });

        try {
            obj.prop |= 20;
            return false;
        } catch (e) {
            return e instanceof TypeError && obj.prop === 11;
        }
    }
runTestCase(testcase);
