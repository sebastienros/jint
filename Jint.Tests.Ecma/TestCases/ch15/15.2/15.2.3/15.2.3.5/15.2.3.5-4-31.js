/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-31.js
 * @description Object.create - 'Properties' is a Boolean object that uses Object's [[Get]] method to access own enumerable property (15.2.3.7 step 5.a)
 */


function testcase() {

        var props = new Boolean(false);
        props.prop = {
            value: 12,
            enumerable: true
        };
        var newObj = Object.create({}, props);
        return newObj.hasOwnProperty("prop");
    }
runTestCase(testcase);
