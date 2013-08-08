/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-32.js
 * @description Object.defineProperty - 'enumerable' property in 'Attributes' is an inherited accessor property without a get function (8.10.5 step 3.a)
 */


function testcase() {
        var obj = {};
        var accessed = false;
        var proto = {};

        Object.defineProperty(proto, "enumerable", {
            set: function () { }
        });

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;

        var child = new ConstructFun();

        Object.defineProperty(obj, "property", child);

        for (var prop in obj) {
            if (prop === "property") {
                accessed = true;
            }
        }
        return !accessed;
    }
runTestCase(testcase);
