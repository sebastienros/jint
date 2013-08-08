/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-30.js
 * @description Object.defineProperty - 'enumerable' property in 'Attributes' is own accessor property without a get function (8.10.5 step 3.a)
 */


function testcase() {
        var obj = {};
        var accessed = false;

        var attr = {};
        Object.defineProperty(attr, "enumerable", {
            set: function () { }
        });

        Object.defineProperty(obj, "property", attr);

        for (var prop in obj) {
            if (prop === "property") {
                accessed = true;
            }
        }
        return !accessed;
    }
runTestCase(testcase);
