/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-228.js
 * @description Object.defineProperties - 'set' property of 'descObj' is own data property (8.10.5 step 8.a)
 */


function testcase() {
        var data = "data";
        var obj = {};

        Object.defineProperties(obj, {
            descObj: {
                set: function (value) {
                    data = value;
                }
            }
        });

        obj.descObj = "overrideData";

        return obj.hasOwnProperty("descObj") && data === "overrideData";
    }
runTestCase(testcase);
