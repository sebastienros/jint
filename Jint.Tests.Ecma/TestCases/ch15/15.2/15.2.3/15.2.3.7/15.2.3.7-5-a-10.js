/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-a-10.js
 * @description Object.defineProperties - 'Properties' is a Boolean object which implements its own [[Get]] method to get enumerable own property
 */


function testcase() {

        var obj = {};
        var props = new Boolean(false);

        Object.defineProperty(props, "prop", {
            value: {
                value: 10
            },
            enumerable: true
        });
        Object.defineProperties(obj, props);

        return obj.hasOwnProperty("prop") && obj.prop === 10;
    }
runTestCase(testcase);
