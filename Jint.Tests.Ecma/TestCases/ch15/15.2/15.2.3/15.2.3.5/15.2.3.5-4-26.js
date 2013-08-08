/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-26.js
 * @description Object.create - TypeError is thrown when own enumerable accessor property of 'Properties' without a get function (15.2.3.7 step 5.a)
 */


function testcase() {

        var props = {};
        Object.defineProperty(props, "prop", {
            set: function () { },
            enumerable: true
        });
        try {
            Object.create({}, props);

            return false;
        } catch (ex) {
            return ex instanceof TypeError;
        }
    }
runTestCase(testcase);
