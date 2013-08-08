/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-43.js
 * @description Object.getOwnPropertyDescriptor - argument 'P' is an object which has an own valueOf method
 */


function testcase() {
        var obj = { "[object Object]": 1, "abc" : 2 };

        var ownProp = {
            valueOf: function () {
                return "abc";
            }
        };

        var desc = Object.getOwnPropertyDescriptor(obj, ownProp);

        return desc.value === 1;
    }
runTestCase(testcase);
