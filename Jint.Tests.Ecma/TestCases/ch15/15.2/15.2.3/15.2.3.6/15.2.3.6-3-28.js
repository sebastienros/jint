/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-28.js
 * @description Object.defineProperty - 'enumerable' property in 'Attributes' is own accessor property that overrides an inherited data property (8.10.5 step 3.a)
 */


function testcase() {
        var obj = {};
        var accessed = false;

        var proto = { enumerable: false };

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;

        var child = new ConstructFun();

        Object.defineProperty(child, "enumerable", {
            get: function () {
                return true;
            }
        });

        Object.defineProperty(obj, "property", child);

        for (var prop in obj) {
            if (prop === "property") {
                accessed = true;
            }
        }
        return accessed;
    }
runTestCase(testcase);
