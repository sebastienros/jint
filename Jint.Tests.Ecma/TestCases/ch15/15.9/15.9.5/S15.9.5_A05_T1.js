// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * The Date.prototype has the property "toLocaleString"
 *
 * @path ch15/15.9/15.9.5/S15.9.5_A05_T1.js
 * @description The Date.prototype has the property "toLocaleString"
 */

if(Date.prototype.hasOwnProperty("toLocaleString") !== true){
  $ERROR('#1: The Date.prototype has the property "toLocaleString"');
}


