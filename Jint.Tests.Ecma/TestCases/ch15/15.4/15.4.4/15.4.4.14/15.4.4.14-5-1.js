/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-1.js
 * @description Array.prototype.indexOf when fromIndex is string
 */


function testcase() {
  var a = [1,2,1,2,1,2];
  if (a.indexOf(2,"2") === 3 &&          // "2" resolves to 2  
      a.indexOf(2,"one") === 1) {       // "one" resolves to 0
    return true;
  }
 }
runTestCase(testcase);
