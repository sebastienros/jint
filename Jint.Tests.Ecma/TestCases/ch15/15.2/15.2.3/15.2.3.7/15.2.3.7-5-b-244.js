/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-244.js
 * @description Object.defineProperties - 'descObj' is the Math object which implements its own [[Get]] method to get 'set' property (8.10.5 step 8.a)
 */


function testcase() {

        var data = "data";
        var setFun = function (value) {
            data = value;
        };
        try {
            Math.prop = {
                set: setFun
            };

            var obj = {};
            Object.defineProperties(obj, Math);
            obj.prop = "mathData";
            return obj.hasOwnProperty("prop") && data === "mathData";
        } finally {
            delete Math.prop;
        }
    }
runTestCase(testcase);
