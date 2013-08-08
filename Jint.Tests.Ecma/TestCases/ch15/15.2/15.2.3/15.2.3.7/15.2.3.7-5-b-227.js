/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-227.js
 * @description Object.defineProperties - 'set' property of 'descObj' is not present (8.10.5 step 8)
 */


function testcase() {
        var data = "data";
        var obj = {};

        try {
            Object.defineProperties(obj, {
                descObj: {
                    get: function () {
                        return data;
                    }
                }
            });


            obj.descObj = "overrideData";

            var desc = Object.getOwnPropertyDescriptor(obj, "descObj");
            return obj.hasOwnProperty("descObj") && typeof (desc.set) === "undefined" && data === "data";
        } catch (e) {
            return false;
        }

    }
runTestCase(testcase);
