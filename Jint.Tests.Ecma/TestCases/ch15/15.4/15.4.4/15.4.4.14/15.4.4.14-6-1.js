/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-6-1.js
 * @description Array.prototype.indexOf returns -1 if fromIndex is greater than Array length
 */


function testcase() {
  var a = [1,2,3];
  if (a.indexOf(1,5) === -1 &&  
     a.indexOf(1,3) === -1  &&
     [ ].indexOf(1,0) === -1  ){
    return true;
  }
 }
runTestCase(testcase);
