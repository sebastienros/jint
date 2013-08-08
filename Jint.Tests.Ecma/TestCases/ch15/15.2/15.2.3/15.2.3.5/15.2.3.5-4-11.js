/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-11.js
 * @description Object.create - argument 'Properties' is a Date object (15.2.3.7 step 2)
 */


function testcase() {

        var props = new Date();
        var result = false;

        Object.defineProperty(props, "prop", {
            get: function () {
                result = this instanceof Date;
                return {};
            },
            enumerable: true
        });
        var newObj = Object.create({}, props);
        return result && newObj.hasOwnProperty("prop");
    }
runTestCase(testcase);
