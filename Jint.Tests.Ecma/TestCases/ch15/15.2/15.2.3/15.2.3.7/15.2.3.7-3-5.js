/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-3-5.js
 * @description Object.defineProperties - own accessor property of 'Properties' which is not enumerable is not defined in 'O' 
 */


function testcase() {

        var obj = {};

        var props = {};

        Object.defineProperty(props, "prop", {
            get: function () {
                return {};
            },
            enumerable: false
        });

        Object.defineProperties(obj, props);

        return !obj.hasOwnProperty("prop");
    }
runTestCase(testcase);
