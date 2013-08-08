/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-17-s.js
 * @description Strict Mode - Function code that is part of a Accessor PropertyAssignment is in Strict Mode if Accessor PropertyAssignment is contained in use strict(getter)
 * @noStrict
 */


function testcase() {
        "use strict";
        try {
            var obj = {};
            Object.defineProperty(obj, "accProperty", {
                get: function () {
                    eval("public = 1;");
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
