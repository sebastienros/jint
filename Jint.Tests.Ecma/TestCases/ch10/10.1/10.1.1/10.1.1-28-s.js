/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-28-s.js
 * @description Strict Mode - Function code of Accessor PropertyAssignment contains Use Strict Directive which appears at the end of the block(setter)
 * @noStrict
 */


function testcase() {
        var obj = {};
        var data;

        Object.defineProperty(obj, "accProperty", {
            set: function (value) {
                var _10_1_1_28_s = {a:1, a:2};
                data = value;
                "use strict";
            }
        });
        obj.accProperty = "overrideData";
        return data==="overrideData";
    }
runTestCase(testcase);
