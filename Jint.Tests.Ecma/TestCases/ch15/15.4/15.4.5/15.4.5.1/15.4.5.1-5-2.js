/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.5/15.4.5.1/15.4.5.1-5-2.js
 * @description Defining a property named 4294967295 (2**32-1) doesn't change length of the array
 */


function testcase() {  
  var a =[0,1,2];
  a[4294967295] = "not an array element" ;
  return a.length===3;
 }
runTestCase(testcase);
