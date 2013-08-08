/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-26-s.js
 * @description Strict Mode - Function code of Accessor PropertyAssignment contains Use Strict Directive which appears at the start of the block(setter)
 * @noStrict
 */


function testcase() {
        try {
            var obj = {};
            var data = "data";
            Object.defineProperty(obj, "accProperty", {
                set: function (value) {
                    "use strict";
                    eval("var public = 1;");
                    data = value;
                }
            });

            obj.accProperty = "overrideData";

            return false;
        } catch (e) {
            return e instanceof SyntaxError && data === "data";
        }
    }
runTestCase(testcase);
