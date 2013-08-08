/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-262.js
 * @description Object.defineProperties - TypeError is thrown if both 'set' property and 'writable' property of 'descObj' are present (8.10.5 step 9.a)
 */


function testcase() {

        var setFun = function () { };
        var obj = {};

        try {
            Object.defineProperties(obj, {
                prop: {
                    writable: true,
                    set: setFun
                }
            });
            return false;
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
