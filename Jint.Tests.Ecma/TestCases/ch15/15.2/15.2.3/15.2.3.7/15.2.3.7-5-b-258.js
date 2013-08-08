/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-258.js
 * @description Object.defineProperties - value of 'set' property of 'descObj' is a function (8.10.5 step 8.b)
 */


function testcase() {

        var data = "data";
        var setFun = function (value) {
            data = value;
        };
        var obj = {};


        Object.defineProperties(obj, {
            prop: {
                set: setFun
            }
        });
        obj.prop = "funData";
        return obj.hasOwnProperty("prop") && data === "funData";
    }
runTestCase(testcase);
