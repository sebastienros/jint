/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-238.js
 * @description Object.getOwnPropertyDescriptor - ensure that 'configurable' property of returned object is data property with correct 'configurable' attribute
 */


function testcase() {
        var obj = { "property": "ownDataProperty" };

        var desc = Object.getOwnPropertyDescriptor(obj, "property");

        var propDefined = "configurable" in desc;
        
        try {
            delete desc.configurable;
            var propDeleted = "configurable" in desc;

            return propDefined && !propDeleted;
        } catch (e) {
            return false;
        }
    }
runTestCase(testcase);
