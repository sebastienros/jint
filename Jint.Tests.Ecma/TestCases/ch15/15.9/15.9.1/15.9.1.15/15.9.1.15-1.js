/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.9/15.9.1/15.9.1.15/15.9.1.15-1.js
 * @description Date Time String Format - specified default values will be set for all optional fields(MM, DD, mm, ss and time zone) when they are absent
 */


function testcase() {
        var result = false;
        var expectedDateTimeStr = "1970-01-01T00:00:00.000Z";
        var dateObj = new Date("1970");
        var dateStr = dateObj.toISOString();
        result = dateStr === expectedDateTimeStr;
        return result;
    }
runTestCase(testcase);
