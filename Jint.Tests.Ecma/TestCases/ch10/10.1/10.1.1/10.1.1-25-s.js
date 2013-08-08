/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-25-s.js
 * @description Strict Mode - Function code of Accessor PropertyAssignment contains Use Strict Directive which appears at the start of the block(getter)
 * @noStrict
 */


function testcase() {
        try {
            var obj = {};
            Object.defineProperty(obj, "accProperty", {
                get: function () {
                    "use strict";
                    eval("var public = 1;");
                    return 11;
                }
            });
            var temp = obj.accProperty === 11;
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
