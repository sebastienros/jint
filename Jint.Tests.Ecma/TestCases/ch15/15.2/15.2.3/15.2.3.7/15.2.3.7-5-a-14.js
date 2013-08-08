/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-14.js
 * @description Object.defineProperties - 'Properties' is a RegExp object which implements its own [[Get]] method to get enumerable own property
 */


function testcase() {

        var obj = {};
        var props = new RegExp();

        Object.defineProperty(props, "prop", {
            value: {
                value: 14
            },
            enumerable: true
        });
        Object.defineProperties(obj, props);

        return obj.hasOwnProperty("prop") && obj.prop === 14;
    }
runTestCase(testcase);
